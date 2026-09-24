// MapLibre GL JS interop for MapLibre component
// Handles map initialization, theme switching, and teardown

import { watchThemeChanges } from './theme.js'
import { computePosition as computeFloatingPosition, applyPosition as applyFloatingPosition } from '../../BlazorBlueprint.Primitives/js/primitives/positioning.js'

const LIGHT_STYLE = "https://tiles.openfreemap.org/styles/positron";
const DARK_STYLE = "https://tiles.openfreemap.org/styles/dark";

/** Gap between the marker dot and its popup, in pixels. */
const POPUP_OFFSET = 12;

/** Half the marker dot's size, used to judge when the dot has fully left the viewport. */
const DOT_REACH = 6;

/**
 * @typedef {Object} MapState
 * @property {Object} map - The MapLibre map instance
 * @property {any} dotNetRef - The .NET object reference to notify about view changes
 * @property {Function} stopWatchingTheme - Unsubscribes from theme changes
 * @property {Map<string, MarkerState>} markers - Blazor-owned marker elements
 * @property {Set<string>} popups - Marker ids whose popup is currently open
 */

/**
 * @typedef {Object} MarkerState
 * @property {number} lng - Longitude of the marker
 * @property {number} lat - Latitude of the marker
 */

/** @type {Map<string, MapState>} */
const mapStates = new Map();

/** @type {Promise|null} */
let maplibreLoadPromise = null;

/** @type {any} */
let maplibregl = null;

/**
 * Lazily load the MapLibre GL JS library that ships with this package.
 *
 * Same single-flight shape as the Quill and ECharts loaders. MapLibre's dist build is ESM,
 * so a dynamic import() returns the module namespace directly.
 *
 * A host that already loads its own MapLibre keeps it — the global is checked first, so the
 * bundled copy is never fetched and the two cannot both own a worker pool.
 *
 * @returns {Promise<any>}
 */
async function loadMapLibre() {
    if (maplibregl) return maplibregl;

    if (window.maplibregl) {
        maplibregl = window.maplibregl;
        return maplibregl;
    }

    if (!maplibreLoadPromise) {
        maplibreLoadPromise = (async () => {
            // MapLibre needs its stylesheet to lay the map out. It stays unlayered, as it is
            // when a host loads it: the themed overrides in blazorblueprint.css are !important
            // inside @layer components, which beats unlayered either way.
            importCss();

            // Resolve relative to this module's own URL
            return await import(new URL('../lib/maplibre/maplibre-gl.mjs', import.meta.url).href);
        })();
    }

    maplibregl = await maplibreLoadPromise;
    return maplibregl;
}

function importCss() {
    const url = new URL('../lib/maplibre/maplibre-gl.min.css', import.meta.url);
    if (document.querySelector(`link[rel="stylesheet"][href="${url}"]`)) {
        return;
    }

    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = url;
    document.head.appendChild(link);
}

function applyStyle(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }
    const isDark = document.documentElement.classList.contains('dark');
    state.map.setStyle(isDark ? DARK_STYLE : LIGHT_STYLE);
}

/**
 * Handles a camera change (pan, zoom, rotate) coming from the map engine by
 * reporting the new position back to .NET, which surfaces it through the
 * @bind-Center and @bind-Zoom callbacks.
 * @param {string} mapId
 */
function notifyViewChanged(mapId) {
    const state = mapStates.get(mapId);
    if (!state || !state.dotNetRef) {
        return;
    }

    const center = state.map.getCenter();
    state.dotNetRef.invokeMethodAsync('OnMapViewChanged', center.lat, center.lng, state.map.getZoom());
}

/**
 * Initializes a MapLibre map instance.
 * @param {string} mapId - Unique identifier for the map; also the container element's id
 * @param {any} dotNetRef - The .NET object reference to notify about view changes
 * @param {Object} options - Additional MapLibre Map options
 * @returns {Promise<Object|null>} The MapLibre map instance, or null on failure
 */
export async function initializeMapLibre(mapId, dotNetRef, options = {}) {
    if (!mapId) {
        console.error('initializeMapLibre: missing required parameters');
        return null;
    }

    const element = document.getElementById(mapId);
    if (!element) {
        console.error(`initializeMapLibre: container #${mapId} not found`);
        return null;
    }

    let mod;
    try {
        mod = await loadMapLibre();
    } catch (err) {
        console.error('Failed to load MapLibre GL JS:', err);
        return null;
    }

    const map = new mod.Map({
        container: element,
        style: LIGHT_STYLE,
        ...options
    });

    const stopWatchingTheme = watchThemeChanges(() => applyStyle(mapId));
    mapStates.set(mapId, { map, dotNetRef, stopWatchingTheme, markers: new Map(), popups: new Set() });

    map.on('moveend', () => notifyViewChanged(mapId));
    map.on('load', () => notifyViewChanged(mapId));
    map.on('move', () => updateMarkers(mapId));
    map.on('load', () => updateMarkers(mapId));

    applyStyle(mapId);

    return map;
}

/**
 * Moves the map camera so the given position becomes the center.
 * @param {string} mapId
 * @param {number} lng
 * @param {number} lat
 */
export function setCenter(mapId, lng, lat) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.map.setCenter([lng, lat]);
}

/**
 * Sets the map zoom level.
 * @param {string} mapId
 * @param {number} zoom
 */
export function setZoom(mapId, zoom) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.map.setZoom(zoom);
}

/**
 * Projects every registered marker's coordinate into pixel space and moves its element
 * there with a CSS transform. Called on every camera move so markers stay glued to the
 * geography while the map pans and zooms.
 * @param {string} mapId
 */
function updateMarkers(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.markers.forEach((marker, markerId) => {
        const point = state.map.project([marker.lng, marker.lat]);
        if (!point || point.x === undefined || point.y === undefined) {
            return;
        }

        const element = document.getElementById(markerId);
        if (!element) {
            return;
        }

        element.style.transform = `translate(${point.x}px, ${point.y}px)`;
        element.style.visibility = 'visible';

        if (state.popups.has(markerId)) {
            positionMarkerPopup(mapId, markerId);
        }
    });
}

/**
 * Keeps an open marker popup inside the map viewport while the marker's dot is on screen,
 * using the same flip + shift logic the Popover primitive applies via Floating UI. Once the
 * dot has left the map, the popup parks at its natural spot relative to the dot instead, so
 * it follows the marker off-screen rather than staying pinned to the map edge.
 * @param {string} mapId
 * @param {string} markerId
 */
function positionMarkerPopup(mapId, markerId) {
    const state = mapStates.get(mapId);
    const marker = state && state.markers.get(markerId);
    const wrapper = document.getElementById(markerId);
    const popup = document.getElementById(`${markerId}-popup`);
    if (!state || !marker || !wrapper || !popup) {
        return;
    }

    const container = state.map.getContainer();
    const point = state.map.project([marker.lng, marker.lat]);
    if (!point || point.x === undefined || point.y === undefined) {
        return;
    }

    const dotInView =
        point.x >= -DOT_REACH && point.x <= container.clientWidth + DOT_REACH &&
        point.y >= -DOT_REACH && point.y <= container.clientHeight + DOT_REACH;

    if (!dotInView) {
        // Dot has left the map. Anchor the popup above the dot with no viewport clamping, so
        // the marker's transform carries it off-screen alongside the dot.
        const rect = popup.getBoundingClientRect();
        applyFloatingPosition(popup, {
            x: -(rect.width / 2),
            y: -(rect.height + POPUP_OFFSET),
            strategy: 'absolute'
        }, true);
        return;
    }

    computeFloatingPosition(wrapper, popup, {
        placement: 'top',
        offset: POPUP_OFFSET,
        flip: true,
        shift: true,
        padding: POPUP_OFFSET,
        strategy: 'absolute'
    }).then(result => {
        applyFloatingPosition(popup, result, true);
    }).catch(() => {
        // Elements not ready yet - the next camera move retries.
    });
}

/**
 * Toggles popup tracking for a marker and positions it immediately. While the popup stays
 * open, the camera-move handler keeps repositioning it so it remains inside the map viewport
 * for as long as the dot is visible.
 * @param {string} mapId
 * @param {string} markerId
 * @param {boolean} open
 */
export function setMarkerPopupOpen(mapId, markerId, open) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    if (open) {
        state.popups.add(markerId);
        positionMarkerPopup(mapId, markerId);
    } else {
        state.popups.delete(markerId);
    }
}

/**
 * Registers a Blazor-rendered marker element with a map so it follows the camera.
 * @param {string} mapId
 * @param {string} markerId - The id of the marker element in the DOM
 * @param {number} lng
 * @param {number} lat
 */
export function registerMarker(mapId, markerId, lng, lat) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.markers.set(markerId, { lng, lat });
    updateMarkers(mapId);
}

/**
 * Moves an already registered marker to a new coordinate.
 * @param {string} mapId
 * @param {string} markerId
 * @param {number} lng
 * @param {number} lat
 */
export function updateMarker(mapId, markerId, lng, lat) {
    const state = mapStates.get(mapId);
    if (!state || !state.markers.has(markerId)) {
        return;
    }

    state.markers.set(markerId, { lng, lat });
    updateMarkers(mapId);
}

/**
 * Stops tracking a marker element so the map ignores it on the next camera move.
 * @param {string} mapId
 * @param {string} markerId
 */
export function unregisterMarker(mapId, markerId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.markers.delete(markerId);
    state.popups.delete(markerId);
}

/**
 * Disposes of a map instance
 * @param {string} mapId - Unique identifier for the map
 */
export function disposeMap(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    // Drop the .NET reference before tearing down so no view callback is
    // delivered during remove() - it may outlive the owning component.
    state.dotNetRef = null;

    // Stop reacting to theme changes, then tear down the map itself
    state.stopWatchingTheme();
    state.map.remove();

    mapStates.delete(mapId);
}
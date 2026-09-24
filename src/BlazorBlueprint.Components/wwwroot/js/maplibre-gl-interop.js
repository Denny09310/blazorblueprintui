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
 * @property {Map<string, RouteState>} routes - Lines drawn as native MapLibre style layers
 */

/**
 * @typedef {Object} MarkerState
 * @property {number} lng - Longitude of the marker
 * @property {number} lat - Latitude of the marker
 */

/**
 * @typedef {Object} RouteState
 * @property {Array<Array<number>>} coordinates - GeoJSON [lng, lat] pairs of the route line
 * @property {string} color - CSS color of the line
 * @property {number} width - Line width in pixels
 * @property {number} opacity - Line opacity from 0 to 1
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
    mapStates.set(mapId, { map, dotNetRef, stopWatchingTheme, markers: new Map(), popups: new Set(), routes: new Map() });

    map.on('moveend', () => notifyViewChanged(mapId));
    map.on('load', () => notifyViewChanged(mapId));
    map.on('move', () => updateMarkers(mapId));
    map.on('load', () => updateMarkers(mapId));
    map.on('load', () => redrawRoutes(mapId));
    element.addEventListener('fullscreenchange', () => handleFullscreenChange(mapId));

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
 * Builds the GeoJSON Feature a route's line source points at.
 * @param {RouteState} route
 * @returns {Object}
 */
function routeData(route) {
    return {
        type: 'Feature',
        properties: {},
        geometry: { type: 'LineString', coordinates: route.coordinates }
    };
}

/**
 * Adds a route's GeoJSON line source and layer, or refreshes them in place when they already
 * exist. Routes are native map layers rather than DOM, so MapLibre projects, clips and draws
 * them for free. The style is not always ready (the map may still be loading, or a theme switch
 * may be mid-flight), in which case the map's 'load' handler retries for every route at once.
 * @param {string} mapId
 * @param {string} routeId
 */
function ensureRouteLayer(mapId, routeId) {
    const state = mapStates.get(mapId);
    const route = state && state.routes.get(routeId);
    if (!state || !route) {
        return;
    }

    const map = state.map;
    if (!map.isStyleLoaded()) {
        return;
    }

    const layerId = `${routeId}-layer`;
    const sourceId = `${routeId}-source`;
    const data = routeData(route);

    if (map.getSource(sourceId)) {
        map.getSource(sourceId).setData(data);
    } else {
        map.addSource(sourceId, { type: 'geojson', data });
        map.addLayer({
            id: layerId,
            type: 'line',
            source: sourceId,
            layout: { 'line-cap': 'round', 'line-join': 'round' },
            paint: {
                'line-color': route.color,
                'line-width': route.width,
                'line-opacity': route.opacity
            }
        });
        return;
    }

    // Style-only changes still need the paint properties refreshed.
    map.setPaintProperty(layerId, 'line-color', route.color);
    map.setPaintProperty(layerId, 'line-width', route.width);
    map.setPaintProperty(layerId, 'line-opacity', route.opacity);
}

/**
 * Re-adds every registered route to the current style. A theme switch replaces the style,
 * taking all custom layers with it, so routes are restored from their tracked state instead.
 * @param {string} mapId
 */
function redrawRoutes(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.routes.forEach((route, routeId) => ensureRouteLayer(mapId, routeId));
}

/**
 * Registers a route line on the map. The coordinates are GeoJSON [lng, lat] pairs.
 * @param {string} mapId
 * @param {string} routeId
 * @param {Array<Array<number>>} coordinates
 * @param {Object} [options] - Route appearance: color, width, opacity
 */
export function registerRoute(mapId, routeId, coordinates, options = {}) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.routes.set(routeId, {
        coordinates,
        color: options.color ?? '#3b82f6',
        width: options.width ?? 3,
        opacity: options.opacity ?? 1
    });

    ensureRouteLayer(mapId, routeId);
}

/**
 * Updates an already registered route's path and appearance.
 * @param {string} mapId
 * @param {string} routeId
 * @param {Array<Array<number>>} coordinates
 * @param {Object} [options] - Route appearance: color, width, opacity
 */
export function updateRoute(mapId, routeId, coordinates, options = {}) {
    registerRoute(mapId, routeId, coordinates, options);
}

/**
 * Removes a route's layer and source from the map and stops tracking it.
 * @param {string} mapId
 * @param {string} routeId
 */
export function unregisterRoute(mapId, routeId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.routes.delete(routeId);

    const map = state.map;
    const layerId = `${routeId}-layer`;
    const sourceId = `${routeId}-source`;

    // Safe no-ops while the style is still loading.
    if (map.getLayer(layerId)) {
        map.removeLayer(layerId);
    }

    if (map.getSource(sourceId)) {
        map.removeSource(sourceId);
    }
}

/**
 * Zooms the map in one level.
 * @param {string} mapId
 */
export function zoomIn(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.map.zoomIn();
}

/**
 * Zooms the map out one level.
 * @param {string} mapId
 */
export function zoomOut(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.map.zoomOut();
}

/**
 * Rotates the map back to north (zero bearing).
 * @param {string} mapId
 */
export function resetNorth(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    state.map.resetNorth();
}

/**
 * Locates the device and eases the camera to it at street level. The browser's permission
 * prompt gates the user consent; failures are logged and leave the camera untouched.
 * @param {string} mapId
 */
export function locateUser(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    if (!('geolocation' in navigator)) {
        console.error('This browser does not support geolocation.');
        return;
    }

    navigator.geolocation.getCurrentPosition(
        position => {
            const { longitude, latitude } = position.coords;
            state.map.easeTo({
                center: [longitude, latitude],
                zoom: Math.max(state.map.getZoom(), 15)
            });
        },
        error => console.error('Geolocation failed:', error.message),
        { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 }
    );
}

/**
 * Toggles the map container between filling the viewport and its normal size. The actual
 * state change arrives through the container's fullscreenchange event (handleFullscreenChange),
 * which does the size swap, the map resize, and the .NET notification - so exiting with Esc
 * stays in sync too.
 * @param {string} mapId
 */
export function toggleFullscreen(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    const container = state.map.getContainer();
    const isFullscreen = document.fullscreenElement === container;

    if (isFullscreen) {
        document.exitFullscreen();
    } else {
        container.requestFullscreen()
            .catch(error => console.error('Fullscreen request failed:', error));
    }
}

/**
 * Handles the map container entering or leaving browser fullscreen. The container's inline
 * size is overridden so the map fills the screen (the fullscreen element is sized to the
 * viewport), then restored on exit. The map is resized so the engine re-measures its canvas,
 * and .NET is notified so the toolbar can swap its maximize/minimize icon.
 * @param {string} mapId
 */
function handleFullscreenChange(mapId) {
    const state = mapStates.get(mapId);
    if (!state) {
        return;
    }

    const container = state.map.getContainer();
    const isFullscreen = document.fullscreenElement === container;

    if (isFullscreen) {
        container.dataset.bbMapStyle = container.style.cssText;
        container.style.width = '100vw';
        container.style.height = '100vh';
    } else if (container.dataset.bbMapStyle !== undefined) {
        container.style.cssText = container.dataset.bbMapStyle;
        delete container.dataset.bbMapStyle;
    }

    state.map.resize();

    if (state.dotNetRef) {
        state.dotNetRef.invokeMethodAsync('OnMapFullscreenChanged', isFullscreen);
    }
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
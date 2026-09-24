import * as maplibregl from '../lib/maplibre/maplibre-gl.mjs'
import { watchThemeChanges } from './chart-theme.js'

const LIGHT_STYLE = "https://tiles.openfreemap.org/styles/positron";
const DARK_STYLE = "https://tiles.openfreemap.org/styles/dark";

/** @type {maplibregl.Map} */
let map = null;

export function initialize(container, options = {}) {
    importCss()
    map = new maplibregl.Map({
        container,
        style: LIGHT_STYLE,
        ...options
    });

    watchThemeChanges(applyStyle);
    applyStyle();

    return map;
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

function applyStyle() {
    const isDark = document.documentElement.classList.contains('dark');
    map?.setStyle(isDark ? DARK_STYLE : LIGHT_STYLE);
}
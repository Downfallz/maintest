// The picture kit (ADR 0198, docs/art/page-pictures.md) as the pages use it: the form of picture-form.js, drawing
// from every page's pictures, page-art.js, unless a caller hands it its own `art`. The battle bundle imports the
// form with its own pictures instead (#1016), so it does not carry this module's.
import * as form from './picture-form.js';
import { PAGE_ART } from './page-art.js';

export { FOCUS_STEP, PAGE_ART_BASE, PAGE_HEAD_SIZES, SCENE_FORMS, STRIP_FRAMES, focusAttributes, undressScene } from './picture-form.js';

// An `art` left undefined is no art handed: every page's, as a parameter's default would read it.
const drawn = options => ({ ...options, art: options.art ?? PAGE_ART });

export const pictureAttributes = (name, options = {}) => form.pictureAttributes(name, drawn(options));
export const sourceAttributes = (name, options = {}) => form.sourceAttributes(name, drawn(options));
export const pictureHtml = (name, options = {}) => form.pictureHtml(name, drawn(options));
export const pictureElement = (doc, name, options = {}) => form.pictureElement(doc, name, drawn(options));
export const stripHtml = (name, options = {}) => form.stripHtml(name, drawn(options));
export const stripElement = (doc, name, options = {}) => form.stripElement(doc, name, drawn(options));
export const sceneHtml = (name, options = {}) => form.sceneHtml(name, drawn(options));
export const sceneElement = (doc, name, options = {}) => form.sceneElement(doc, name, drawn(options));
export const wearScene = (doc, element, name, options = {}) => form.wearScene(doc, element, name, drawn(options));
export const wearPageHead = (doc, name, options = {}) => form.wearPageHead(doc, name, drawn(options));

# ADR-0007: Canvas-based rendering of keypoints and matches

- Status: Accepted
- Date: 2026-10-20

## Context

The design mockup draws each keypoint as a DOM element. Real images produce thousands of keypoints, and matching draws hundreds of lines. With zoom, pan and live threshold sliders, one DOM node per primitive becomes the bottleneck.

## Decision

- The image viewer (`ImageStage`) is a stack of layers inside one container that is transformed for zoom and pan (CSS `transform: matrix(...)`):
  1. a base `<img>`;
  2. an optional heatmap `<img>` with adjustable opacity;
  3. a `<canvas>` for keypoints or match lines, redrawn in a `requestAnimationFrame` callback when data, filters or zoom change;
  4. a small DOM overlay for the selected keypoint and tooltips, so they stay crisp and accessible.
- The canvas is sized to the device pixel ratio. Stroke widths are compensated for zoom, so circles keep a constant on-screen line width.
- Hit-testing for clicks and hover uses a uniform grid index over keypoint positions, built once per data set.
- Keyboard access: the stage is focusable. Arrow keys move the selection to the nearest keypoint in that direction, and the inspector has previous and next buttons.

## Consequences

- Rendering stays smooth with tens of thousands of primitives.
- Canvas content is not visible to assistive technology. The inspector and the summary statistics provide the accessible equivalent.
- Hit-testing and the zoom math must be written and tested by hand. A small library for pan and zoom may be adopted if it fits, but the canvas drawing stays ours.

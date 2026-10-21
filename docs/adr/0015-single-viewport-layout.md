# ADR-0015: Every view fits one viewport

- Status: Accepted
- Date: 2026-10-21

## Context

The first version of the UI let each view grow with its content. The Detector reached about 1400 px at 1440 wide, mostly because the keypoint inspector stacked under the algorithm settings in the right column, so the user scrolled to see the image and the controls together. The design mockup ("Salient UI") was revised to remove all scrolling.

## Decision

- The app shell is exactly one viewport tall (`100dvh`, at least 560 px) and clips its own overflow. A view fills what is left under the app bar and sizes its cards to fit; the page never scrolls.
- Image areas fill their card (`ImageStage` with `fill`) and fit the image inside, instead of taking their height from the image's aspect ratio.
- Side columns have a fixed width and scroll internally only when the window is too short for their cards. That is a fallback, not the intended state at 1440 × 900.
- Secondary detail moves behind a control instead of taking space: stage timings open from a chevron in the run bar as a panel over the view, and parameter explanations move into tooltips.
- The run bar is a single line. Stages that do not fit scroll sideways and the running stage is kept in view.
- Detector: the keypoint inspector is a horizontal strip under the image, so the right column holds only Algorithm and Parameters and does not grow when a keypoint is selected.
- Matching: the result moves into a strip under the image pair. Robustness: the pair preview and the value table share the left column, and the chart fills the rest.
- The end-to-end tests assert this (`expectFitsViewport`): no page scroll, and no card clips or scrolls its content.

## Consequences

- Controls stay visible while the image is inspected, and nothing important sits below the fold.
- Image areas are smaller on short windows; zooming with the wheel compensates.
- A new panel must be designed for a fixed height: it either fits, scrolls inside its own column or moves behind a control.

# ADR-0006: Transport of images and feature data

- Status: Accepted
- Date: 2026-10-20

## Context

The UI displays large data:
- pyramid layers, DoG layers and heatmaps (full-resolution images);
- up to several thousand keypoints;
- up to several hundred matches;
- 128-dimensional descriptors.

Sending raw float arrays to the browser is slow and moves image processing into the client.

## Decision

- **Images:** the server renders every image layer to an 8-bit WebP:
  - DoG uses a diverging colormap;
  - heatmaps use viridis or magma;
  - pyramid layers are grayscale.
  
  Layers are served from `GET /api/runs/{id}/layers/{layerId}` with long-lived cache headers, because layer content is immutable for a given run. The run result lists the available layers and their legends (min, max, colormap).
- **Keypoints:** a JSON array of compact objects (`x`, `y`, `scale`, `angle`, `response`, `octave`, `edgeRatio`) from `GET /api/runs/{id}/keypoints`. Thresholds and octave filters are applied on the client to the full candidate set, so the sliders update instantly without a new run.
- **Descriptors and orientation histograms:** fetched lazily for one keypoint at a time with `GET /api/runs/{id}/keypoints/{index}`, when the inspector opens.
- **Matches:** a JSON array of index pairs plus the ratio and an inlier flag. The homography is returned as a 3×3 array.
- Response compression (Brotli) is enabled for JSON.

## Consequences

- The browser only draws. All numerical work stays on the server.
- To support client-side thresholding, the server returns candidates with their contrast and edge ratio, not only the final keypoints. Payloads are therefore larger, which is acceptable at this size.
- Colormaps are fixed on the server. Changing a colormap means re-rendering on the server; there is no live recoloring on the client.

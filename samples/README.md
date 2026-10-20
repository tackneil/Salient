# Sample images

These images are generated procedurally and deterministically. They contain no third-party content, so there is no licensing question. The generators are the same ones the tests use: `GoldenScene` and `SyntheticImages` in `tests/Salient.Features.Sift.Tests`.

| File | Shows |
|------|-------|
| `synthetic-scene.png` | A cluttered scene: soft-edged shapes, textured patches and a gradient. The golden-test scene, at a larger size. |
| `blob-scales.png` | Bright (top) and dark (bottom) Gaussian blobs with σ from 2 to 18. SIFT finds each at a scale proportional to its size, as minima or maxima of the signed DoG. |
| `geometry.png` | Rectangles, disks and a ring. Blobs and corners survive; responses along straight edges are rejected. |

Real photographs can be added here as PNG, JPEG or WebP files. They then appear in the app's sample gallery. Include only images you have the right to redistribute, and note their source and license in this file.

import { EmptyState } from '../../shared/components/EmptyState'

/** Matching view; implemented in Phase 8. */
export function MatchingPage() {
  return (
    <EmptyState title="Matching">
      Match two images and estimate the homography between them with RANSAC.
    </EmptyState>
  )
}

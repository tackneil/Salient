import { EmptyState } from '../../shared/components/EmptyState'

/** Pipeline view; implemented in Phase 8. */
export function PipelinePage() {
  return (
    <EmptyState title="Pipeline">
      Step through every stage of SIFT for the current detection run: the Gaussian pyramid, DoG,
      extrema and each filter.
    </EmptyState>
  )
}

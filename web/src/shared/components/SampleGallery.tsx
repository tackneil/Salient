import Box from '@mui/material/Box'
import ButtonBase from '@mui/material/ButtonBase'
import Typography from '@mui/material/Typography'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { api, unwrap, type ImageInfo } from '../api/client'
import { queryKeys } from '../api/queries'

/** Bundled sample images; choosing one imports it like an upload. */
export function SampleGallery({ onImported }: { onImported: (image: ImageInfo) => void }) {
  const client = useQueryClient()
  const samples = useQuery({
    queryKey: ['samples'],
    queryFn: () => unwrap(api.GET('/api/samples')),
    staleTime: Infinity,
  })
  const importSample = useMutation({
    mutationFn: (name: string) =>
      unwrap(api.POST('/api/samples/{name}', { params: { path: { name } } })),
    onSuccess: (image) => {
      client.setQueryData(queryKeys.image(image.id), image)
      onImported(image)
    },
  })

  if (!samples.data || samples.data.length === 0) return null

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, alignItems: 'center' }}>
      <Typography variant="caption" color="text.secondary">
        Or try a sample
      </Typography>
      <Box
        role="group"
        aria-label="Sample images"
        sx={{ display: 'flex', flexWrap: 'wrap', gap: 1.5, justifyContent: 'center' }}
      >
        {samples.data.map((sample) => (
          <ButtonBase
            key={sample.name}
            aria-label={`Use sample: ${sample.title}`}
            disabled={importSample.isPending}
            onClick={() => {
              importSample.mutate(sample.name)
            }}
            sx={{
              flexDirection: 'column',
              gap: 0.75,
              p: 1,
              borderRadius: 1.5,
              border: 1,
              borderColor: 'divider',
              '&:hover, &:focus-visible': { borderColor: 'primary.main' },
            }}
          >
            <Box
              component="img"
              src={`/api/samples/${encodeURIComponent(sample.name)}/content`}
              alt=""
              sx={{
                width: 160,
                height: 106,
                objectFit: 'cover',
                borderRadius: 1,
                display: 'block',
              }}
            />
            <Typography sx={{ fontSize: 13 }}>{sample.title}</Typography>
          </ButtonBase>
        ))}
      </Box>
    </Box>
  )
}

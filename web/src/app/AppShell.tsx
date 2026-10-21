import UploadIcon from '@mui/icons-material/FileUploadOutlined'
import Alert from '@mui/material/Alert'
import AppBar from '@mui/material/AppBar'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Snackbar from '@mui/material/Snackbar'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Toolbar from '@mui/material/Toolbar'
import Tooltip from '@mui/material/Tooltip'
import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'

import { useUploadImage } from '../shared/api/queries'
import { ensureStarted, useConnectionStatus } from '../shared/api/realtime'
import { SalientLogo } from '../shared/components/SalientLogo'
import { tokens } from './theme'
import { useWorkspace } from './workspace'

const sections = [
  { path: '/detector', label: 'Detector' },
  { path: '/pipeline', label: 'Pipeline' },
  { path: '/matching', label: 'Matching' },
  { path: '/robustness', label: 'Robustness' },
] as const

function LiveIndicator() {
  const status = useConnectionStatus((s) => s.status)
  const live = status === 'connected'
  return (
    <Tooltip
      title={
        live
          ? 'Progress is streamed from the server over SignalR'
          : `SignalR ${status}; falling back to polling`
      }
    >
      <Box
        role="status"
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1,
          height: 28,
          px: 1.25,
          borderRadius: 3.5,
          bgcolor: live ? '#15241c' : '#2a2418',
          color: live ? '#8fe0b0' : '#ffcf8a',
          fontSize: 12,
          fontWeight: 500,
        }}
      >
        <Box
          sx={{
            width: 8,
            height: 8,
            borderRadius: '50%',
            bgcolor: live ? tokens.success : tokens.keypoint,
            '@keyframes pulse': {
              '0%': { boxShadow: '0 0 0 0 rgba(95,208,143,.6)' },
              '70%': { boxShadow: '0 0 0 6px rgba(95,208,143,0)' },
              '100%': { boxShadow: '0 0 0 0 rgba(95,208,143,0)' },
            },
            animation: live ? 'pulse 2s infinite' : 'none',
          }}
        />
        {live ? 'Live' : status === 'connecting' ? 'Connecting' : 'Offline'}
      </Box>
    </Tooltip>
  )
}

/** App bar with navigation, connection status and upload; views render in the outlet. */
export function AppShell() {
  const location = useLocation()
  const navigate = useNavigate()
  const fileInput = useRef<HTMLInputElement>(null)
  const upload = useUploadImage()
  const setImage = useWorkspace((s) => s.setImage)
  const [error, setError] = useState<string>()

  useEffect(() => {
    void ensureStarted().catch(() => undefined)
  }, [])

  const current = sections.find((s) => location.pathname.startsWith(s.path))?.path ?? false

  const onFile = (file: File | undefined) => {
    if (!file) return
    upload.mutate(file, {
      onSuccess: (image) => {
        setImage(image.id)
        if (!location.pathname.startsWith('/detector')) void navigate('/detector')
      },
      onError: (e) => {
        setError(e.message)
      },
    })
  }

  return (
    <Box
      sx={{
        height: '100dvh',
        minHeight: 560,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      <AppBar position="static" sx={{ flex: '0 0 auto' }}>
        <Toolbar sx={{ gap: 3, flexWrap: 'nowrap', minHeight: 56 }}>
          <SalientLogo />
          <Tabs value={current} aria-label="Sections" sx={{ alignSelf: 'stretch' }}>
            {sections.map((s) => (
              <Tab key={s.path} label={s.label} value={s.path} component={NavLink} to={s.path} />
            ))}
          </Tabs>
          <Box sx={{ flex: 1 }} />
          <LiveIndicator />
          <input
            ref={fileInput}
            type="file"
            accept="image/png,image/jpeg,image/webp,image/gif,image/bmp"
            hidden
            onChange={(e) => {
              onFile(e.target.files?.[0])
              e.target.value = ''
            }}
          />
          <Button
            variant="outlined"
            color="inherit"
            startIcon={<UploadIcon />}
            onClick={() => fileInput.current?.click()}
            disabled={upload.isPending}
            sx={{ borderColor: tokens.borderStrong }}
          >
            {upload.isPending ? 'Uploading…' : 'Upload image'}
          </Button>
        </Toolbar>
      </AppBar>
      <Box
        component="main"
        sx={{
          flex: 1,
          minHeight: 0,
          display: 'flex',
          flexDirection: 'column',
          width: '100%',
          maxWidth: 1680,
          mx: 'auto',
          px: 2,
          py: 1.5,
          boxSizing: 'border-box',
        }}
      >
        <Outlet />
      </Box>
      <Snackbar
        open={Boolean(error)}
        autoHideDuration={6000}
        onClose={() => {
          setError(undefined)
        }}
      >
        <Alert
          severity="error"
          variant="filled"
          onClose={() => {
            setError(undefined)
          }}
        >
          {error}
        </Alert>
      </Snackbar>
    </Box>
  )
}

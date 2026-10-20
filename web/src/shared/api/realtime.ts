import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { create } from 'zustand'

import type { RunInfo, Schemas, StageInfo } from './client'
import { queryKeys } from './queries'

type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

/** Connection state for the "Live" indicator in the app bar. */
export const useConnectionStatus = create<{ status: ConnectionStatus }>(() => ({
  status: 'connecting',
}))

let connection: HubConnection | undefined
let starting: Promise<void> | undefined

/** The shared hub connection to /hubs/runs, started on first use and reconnected automatically (ADR-0005). */
export function getConnection(): HubConnection {
  if (!connection) {
    const hub = new HubConnectionBuilder()
      .withUrl('/hubs/runs')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    hub.onreconnecting(() => {
      useConnectionStatus.setState({ status: 'reconnecting' })
    })
    hub.onreconnected(() => {
      useConnectionStatus.setState({ status: 'connected' })
    })
    hub.onclose(() => {
      useConnectionStatus.setState({ status: 'disconnected' })
    })
    connection = hub
  }
  return connection
}

export async function ensureStarted(): Promise<HubConnection> {
  const hub = getConnection()
  if (hub.state === HubConnectionState.Disconnected) {
    starting ??= hub
      .start()
      .then(() => {
        useConnectionStatus.setState({ status: 'connected' })
      })
      .catch((error: unknown) => {
        useConnectionStatus.setState({ status: 'disconnected' })
        throw error
      })
      .finally(() => {
        starting = undefined
      })
  }
  await starting
  return hub
}

/** Inserts or replaces a stage in a run's cached state. */
export function upsertStage(run: RunInfo, stage: StageInfo): RunInfo {
  const index = run.stages.findIndex((s) => s.id === stage.id)
  const stages =
    index >= 0 ? run.stages.map((s, i) => (i === index ? stage : s)) : [...run.stages, stage]
  return { ...run, status: run.status === 'queued' ? 'running' : run.status, stages }
}

function updateRun(client: QueryClient, runId: string, update: (run: RunInfo) => RunInfo) {
  client.setQueryData<RunInfo>(queryKeys.run(runId), (run) => (run ? update(run) : run))
}

/**
 * Subscribes to a run's live events and mirrors them into the TanStack Query cache, so components only read
 * `useRun(runId)`. The hub replays the current state on subscribe, so a late subscriber misses nothing.
 */
export function useRunProgress(runId: string | undefined) {
  const client = useQueryClient()

  useEffect(() => {
    if (!runId) return undefined
    let disposed = false
    const hub = getConnection()

    const matches = (id: string) => id === runId
    const onStageStarted = (id: string, stageId: string) => {
      if (!matches(id)) return
      updateRun(client, runId, (run) =>
        upsertStage(run, {
          id: stageId,
          state: 'running',
          durationMs: null,
          input: 0,
          kept: 0,
          rejected: 0,
        }),
      )
    }
    const onStageCompleted = (id: string, stage: StageInfo) => {
      if (matches(id)) updateRun(client, runId, (run) => upsertStage(run, stage))
    }
    const onProgress = (id: string, progress: Schemas['RunProgress']) => {
      if (matches(id)) updateRun(client, runId, (run) => ({ ...run, progress }))
    }
    const onFinished = (id: string) => {
      if (matches(id)) void client.invalidateQueries({ queryKey: queryKeys.run(runId) })
    }

    hub.on('StageStarted', onStageStarted)
    hub.on('StageCompleted', onStageCompleted)
    hub.on('Progress', onProgress)
    hub.on('RunCompleted', onFinished)
    hub.on('RunFailed', onFinished)
    hub.on('RunCanceled', onFinished)

    void ensureStarted()
      .then((h) => h.invoke<RunInfo | null>('Subscribe', runId))
      .then((snapshot) => {
        if (!disposed && snapshot) client.setQueryData(queryKeys.run(runId), snapshot)
      })
      .catch(() => {
        // Polling in useRun keeps the view current without a hub connection.
      })

    return () => {
      disposed = true
      hub.off('StageStarted', onStageStarted)
      hub.off('StageCompleted', onStageCompleted)
      hub.off('Progress', onProgress)
      hub.off('RunCompleted', onFinished)
      hub.off('RunFailed', onFinished)
      hub.off('RunCanceled', onFinished)
      if (hub.state === HubConnectionState.Connected)
        void hub.invoke('Unsubscribe', runId).catch(() => undefined)
    }
  }, [client, runId])
}

import { spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'
import { createServer } from 'vite'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const server = await createServer({
  root,
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
  },
})

try {
  await server.listen()

  const cliPath = resolve(root, 'node_modules', '@playwright', 'test', 'cli.js')
  const exitCode = await new Promise((resolveExitCode) => {
    const child = spawn(
      process.execPath,
      [cliPath, 'test', ...process.argv.slice(2)],
      {
        cwd: root,
        env: process.env,
        stdio: 'inherit',
      },
    )

    child.on('exit', (code) => resolveExitCode(code ?? 1))
    child.on('error', () => resolveExitCode(1))
  })

  process.exitCode = exitCode
} finally {
  await server.close()
}

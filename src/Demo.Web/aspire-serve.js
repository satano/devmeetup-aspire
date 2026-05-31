// Cross-platform launcher for the Angular dev server. Aspire injects PORT (the port it expects the
// app to listen on); bind ng serve to it. Falls back to 4200 when run standalone via `npm start`.
// npm puts node_modules/.bin on PATH for scripts, so `ng` resolves here (shell: true handles ng.cmd
// on Windows).
const { spawn } = require('node:child_process');

const port = process.env.PORT || '4200';

const child = spawn('ng', ['serve', '--port', port], {
  stdio: 'inherit',
  shell: true,
});

child.on('exit', (code) => process.exit(code ?? 0));

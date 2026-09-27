// Serve the real static assets; catalogue/API responses are supplied by each browser test.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, extname } from 'node:path';
const root = resolve('../..');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css' };
createServer(async (request, response) => {
  const pathname = new URL(request.url, 'http://localhost').pathname.replace(/^\/maintest/, '');
  const table = pathname.startsWith('/table/');
  const asset = table ? pathname.slice('/table'.length) : pathname;
  const relative = asset === '/' ? '/index.html' : asset;
  let base = 'studio';
  if (table) base = 'table';
  else if (relative === '/viewer.css') base = 'viewer';
  const file = resolve(root, base, `.${relative}`);
  if (!file.startsWith(`${root}/${base}/`)) { response.writeHead(404).end(); return; }
  try {
    const content = await readFile(file);
    response.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' }).end(content);
  } catch { response.writeHead(404).end(); }
}).listen(5199, '127.0.0.1');

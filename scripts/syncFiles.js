import fs from 'fs';
import path from 'path';

const rootDir = process.cwd();
const targetDirectories = ['Quorum.Backend', 'Quorum.Backend.AdminUI'];
const rootFiles = ['Quorum.slnx'];

function getCategory(filePath) {
  if (filePath.endsWith('.cs')) return 'csharp';
  if (filePath.endsWith('.cshtml')) return 'razor';
  if (filePath.endsWith('.json') || filePath.endsWith('.csproj') || filePath.endsWith('.slnx') || filePath.endsWith('.props') || filePath.endsWith('.xml')) return 'config';
  if (filePath.endsWith('.md')) return 'docs';
  return 'config';
}

function getDescription(filePath) {
  if (filePath.includes('Gateway/Test')) return 'Widok i logika testera API Gateway (ewaluacja i proxy upstream)';
  if (filePath.includes('GatewayTestModels')) return 'Modele ewaluacji i żądań testowych API Gateway';
  if (filePath.includes('Gateway')) return 'Zarządzanie API Gateway i regułami routingu';
  if (filePath.includes('Federation')) return 'Obsługa dynamicznych federacji OIDC / OAuth2';
  if (filePath.includes('SeedData')) return 'Inicjalizacja i seedowanie bazy danych oraz schema fallback';
  if (filePath.includes('ApplicationDbContext')) return 'Kontekst bazy danych EF Core';
  if (filePath.includes('Program.cs')) return 'Punkt startowy i potok middleware ASP.NET Core';
  if (filePath.endsWith('.csproj')) return 'Plik projektu .NET';
  if (filePath.endsWith('.slnx')) return 'Plik rozwiązania Solution .NET';
  return 'Plik projektu Quorum';
}

const allFiles = [];

function scanDir(dir) {
  if (!fs.existsSync(dir)) return;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    const relPath = path.relative(rootDir, fullPath).replace(/\\/g, '/');

    if (entry.isDirectory()) {
      if (entry.name === 'bin' || entry.name === 'obj' || entry.name === '.git' || entry.name === 'node_modules') continue;
      scanDir(fullPath);
    } else if (entry.isFile()) {
      if (entry.name.endsWith('.db') || entry.name.endsWith('.db-shm') || entry.name.endsWith('.db-wal') || entry.name.endsWith('.log')) continue;
      try {
        const content = fs.readFileSync(fullPath, 'utf8');
        allFiles.push({
          path: relPath,
          name: relPath,
          category: getCategory(relPath),
          description: getDescription(relPath),
          content: content
        });
      } catch (e) {
        console.error(`Error reading ${relPath}:`, e);
      }
    }
  }
}

for (const dir of targetDirectories) {
  scanDir(path.join(rootDir, dir));
}

for (const f of rootFiles) {
  const fullPath = path.join(rootDir, f);
  if (fs.existsSync(fullPath)) {
    allFiles.push({
      path: f,
      name: f,
      category: getCategory(f),
      description: getDescription(f),
      content: fs.readFileSync(fullPath, 'utf8')
    });
  }
}

allFiles.sort((a, b) => a.path.localeCompare(b.path));

const outContent = `export interface ProjectFile {
  path: string;
  name: string;
  category: 'config' | 'csharp' | 'razor' | 'view' | 'docs';
  content: string;
  description: string;
}

export const PROJECT_FILES: ProjectFile[] = ${JSON.stringify(allFiles, null, 2)};
`;

fs.writeFileSync(path.join(rootDir, 'src/data/projectFiles.ts'), outContent, 'utf8');
console.log(`Successfully synchronized ${allFiles.length} project files into src/data/projectFiles.ts`);

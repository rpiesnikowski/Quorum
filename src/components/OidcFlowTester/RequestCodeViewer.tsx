import React, { useState } from 'react';
import { Copy, Check, Terminal, Code2, Globe } from 'lucide-react';

interface RequestCodeViewerProps {
  httpRaw: string;
  curl: string;
  powershell: string;
  csharp: string;
  javascript: string;
  urlPreview?: string;
  method?: string;
}

export const RequestCodeViewer: React.FC<RequestCodeViewerProps> = ({
  httpRaw,
  curl,
  powershell,
  csharp,
  javascript,
  urlPreview,
  method = 'POST'
}) => {
  const [activeCodeTab, setActiveCodeTab] = useState<'raw' | 'curl' | 'powershell' | 'csharp' | 'js'>('raw');
  const [copied, setCopied] = useState(false);

  const getActiveCode = () => {
    switch (activeCodeTab) {
      case 'raw':
        return httpRaw;
      case 'curl':
        return curl;
      case 'powershell':
        return powershell;
      case 'csharp':
        return csharp;
      case 'js':
        return javascript;
      default:
        return httpRaw;
    }
  };

  const handleCopy = () => {
    navigator.clipboard.writeText(getActiveCode());
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="bg-slate-950 border border-slate-800 rounded-xl overflow-hidden shadow-inner flex flex-col">
      {/* Header with Format Tabs and Copy Action */}
      <div className="flex flex-wrap items-center justify-between px-3 py-2 bg-slate-900/90 border-b border-slate-800 gap-2">
        <div className="flex items-center gap-1 overflow-x-auto">
          <button
            onClick={() => setActiveCodeTab('raw')}
            className={`px-2.5 py-1 text-xs rounded-md transition-colors cursor-pointer flex items-center gap-1.5 font-medium ${
              activeCodeTab === 'raw'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
            }`}
          >
            <Globe className="w-3.5 h-3.5" />
            <span>HTTP Raw</span>
          </button>

          <button
            onClick={() => setActiveCodeTab('curl')}
            className={`px-2.5 py-1 text-xs rounded-md transition-colors cursor-pointer flex items-center gap-1.5 font-medium ${
              activeCodeTab === 'curl'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
            }`}
          >
            <Terminal className="w-3.5 h-3.5" />
            <span>cURL</span>
          </button>

          <button
            onClick={() => setActiveCodeTab('powershell')}
            className={`px-2.5 py-1 text-xs rounded-md transition-colors cursor-pointer flex items-center gap-1.5 font-medium ${
              activeCodeTab === 'powershell'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
            }`}
          >
            <Terminal className="w-3.5 h-3.5" />
            <span>PowerShell</span>
          </button>

          <button
            onClick={() => setActiveCodeTab('csharp')}
            className={`px-2.5 py-1 text-xs rounded-md transition-colors cursor-pointer flex items-center gap-1.5 font-medium ${
              activeCodeTab === 'csharp'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
            }`}
          >
            <Code2 className="w-3.5 h-3.5 text-purple-400" />
            <span>C# HttpClient</span>
          </button>

          <button
            onClick={() => setActiveCodeTab('js')}
            className={`px-2.5 py-1 text-xs rounded-md transition-colors cursor-pointer flex items-center gap-1.5 font-medium ${
              activeCodeTab === 'js'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
            }`}
          >
            <Code2 className="w-3.5 h-3.5 text-yellow-400" />
            <span>JavaScript (Fetch)</span>
          </button>
        </div>

        <button
          onClick={handleCopy}
          className="flex items-center gap-1.5 px-2.5 py-1 text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-md transition-colors cursor-pointer border border-slate-700"
          title="Skopiuj kod do schowka"
        >
          {copied ? (
            <>
              <Check className="w-3.5 h-3.5 text-emerald-400" />
              <span className="text-emerald-400 font-medium">Skopiowano!</span>
            </>
          ) : (
            <>
              <Copy className="w-3.5 h-3.5" />
              <span>Kopiuj</span>
            </>
          )}
        </button>
      </div>

      {/* URL preview if provided */}
      {urlPreview && (
        <div className="px-3 py-1.5 bg-slate-900/40 border-b border-slate-800/80 text-[11px] font-mono text-slate-400 flex items-center gap-2 overflow-x-auto">
          <span className="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase bg-blue-500/20 text-blue-400 border border-blue-500/30 shrink-0">
            {method}
          </span>
          <span className="text-slate-300 truncate">{urlPreview}</span>
        </div>
      )}

      {/* Code Body */}
      <div className="p-3 overflow-x-auto max-h-[360px] text-xs font-mono text-slate-300 leading-relaxed whitespace-pre selection:bg-blue-600 selection:text-white">
        {getActiveCode()}
      </div>
    </div>
  );
};

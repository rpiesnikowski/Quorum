import React, { useEffect, useRef, useState, useMemo } from 'react';
import * as d3 from 'd3';
import { 
  Network, 
  ZoomIn, 
  ZoomOut, 
  RotateCcw, 
  Layers, 
  Filter, 
  Shield, 
  User as UserIcon, 
  FileText, 
  Database, 
  Share2, 
  ArrowRight,
  Info,
  Play,
  Maximize2,
  Minimize2,
  CheckCircle2,
  Search
} from 'lucide-react';
import { AuthZenRule } from './OpenFgaManagerTab';

export interface GraphNode extends d3.SimulationNodeDatum {
  id: string;
  label: string;
  type: 'user' | 'role' | 'group' | 'service' | 'resource';
  subType?: string;
  category: 'subject' | 'role' | 'resource';
  degree: number;
}

export interface GraphLink extends d3.SimulationLinkDatum<GraphNode> {
  id: string;
  source: string | GraphNode;
  target: string | GraphNode;
  relation: string;
  effect: 'Permit' | 'Deny';
  ruleName: string;
  ruleId: string;
}

interface OpenFgaGraphViewProps {
  rules: AuthZenRule[];
  onSelectForCheck?: (user: string, relation: string, object: string) => void;
  onOpenCreateRule?: () => void;
}

export const OpenFgaGraphView: React.FC<OpenFgaGraphViewProps> = ({
  rules,
  onSelectForCheck,
  onOpenCreateRule
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const svgRef = useRef<SVGSVGElement>(null);
  const zoomBehaviorRef = useRef<d3.ZoomBehavior<SVGSVGElement, unknown> | null>(null);

  const [layoutMode, setLayoutMode] = useState<'force' | 'hierarchical'>('force');
  const [filterType, setFilterType] = useState<'all' | 'users' | 'roles' | 'resources'>('all');
  const [selectedNode, setSelectedNode] = useState<GraphNode | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [hoveredNode, setHoveredNode] = useState<GraphNode | null>(null);
  const [hoveredLink, setHoveredLink] = useState<GraphLink | null>(null);

  // Transform active rules into nodes and links for Zanzibar model
  const { nodes, links, stats } = useMemo(() => {
    const nodeMap = new Map<string, GraphNode>();
    const linkList: GraphLink[] = [];

    // Filter active Permit rules (or all if specified)
    const activeRules = rules.filter(r => r.isEnabled && r.effect === 'Permit');

    activeRules.forEach(rule => {
      const subjectId = `${rule.subjectType}:${rule.subjectId}`;
      const resourceId = `${rule.resourceType}:${rule.resourceId}`;

      // Source subject node
      if (!nodeMap.has(subjectId)) {
        nodeMap.set(subjectId, {
          id: subjectId,
          label: rule.subjectId,
          type: rule.subjectType,
          category: rule.subjectType === 'user' || rule.subjectType === 'service' ? 'subject' : 'role',
          degree: 0
        });
      }

      // Target resource node
      if (!nodeMap.has(resourceId)) {
        nodeMap.set(resourceId, {
          id: resourceId,
          label: rule.resourceId,
          type: 'resource',
          subType: rule.resourceType,
          category: 'resource',
          degree: 0
        });
      }

      // Increment degree
      nodeMap.get(subjectId)!.degree += 1;
      nodeMap.get(resourceId)!.degree += 1;

      linkList.push({
        id: `${rule.id}-${rule.action}`,
        source: subjectId,
        target: resourceId,
        relation: rule.action.toLowerCase(),
        effect: rule.effect,
        ruleName: rule.name,
        ruleId: rule.id
      });
    });

    const allNodes = Array.from(nodeMap.values());

    return {
      nodes: allNodes,
      links: linkList,
      stats: {
        totalNodes: allNodes.length,
        subjectsCount: allNodes.filter(n => n.category === 'subject').length,
        rolesCount: allNodes.filter(n => n.category === 'role').length,
        resourcesCount: allNodes.filter(n => n.category === 'resource').length,
        tuplesCount: linkList.length
      }
    };
  }, [rules]);

  // Apply visual filtering
  const filteredNodes = useMemo(() => {
    if (filterType === 'all') return nodes;
    if (filterType === 'users') return nodes.filter(n => n.type === 'user' || n.category === 'subject');
    if (filterType === 'roles') return nodes.filter(n => n.category === 'role');
    if (filterType === 'resources') return nodes.filter(n => n.category === 'resource');
    return nodes;
  }, [nodes, filterType]);

  const activeNodeIds = useMemo(() => new Set(filteredNodes.map(n => n.id)), [filteredNodes]);

  const filteredLinks = useMemo(() => {
    return links.filter(link => {
      const sId = typeof link.source === 'string' ? link.source : (link.source as GraphNode).id;
      const tId = typeof link.target === 'string' ? link.target : (link.target as GraphNode).id;
      return activeNodeIds.has(sId) && activeNodeIds.has(tId);
    });
  }, [links, activeNodeIds]);

  // Helper color map
  const getNodeColor = (node: GraphNode) => {
    if (node.category === 'subject') {
      return { fill: '#3b82f6', stroke: '#60a5fa', text: '#bfdbfe', bg: 'rgba(59, 130, 246, 0.15)' };
    }
    if (node.category === 'role') {
      return { fill: '#8b5cf6', stroke: '#a78bfa', text: '#ddd6fe', bg: 'rgba(139, 92, 246, 0.15)' };
    }
    return { fill: '#10b981', stroke: '#34d399', text: '#a7f3d0', bg: 'rgba(16, 185, 129, 0.15)' };
  };

  // Render D3 Graph
  useEffect(() => {
    if (!svgRef.current || !containerRef.current) return;

    const width = containerRef.current.clientWidth || 800;
    const height = isFullscreen ? window.innerHeight - 180 : 540;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    svg
      .attr('width', width)
      .attr('height', height)
      .attr('viewBox', [0, 0, width, height]);

    // Defs for arrowhead markers and gradients
    const defs = svg.append('defs');

    // Arrow markers
    defs.append('marker')
      .attr('id', 'arrowhead-default')
      .attr('viewBox', '0 -5 10 10')
      .attr('refX', 26)
      .attr('refY', 0)
      .attr('markerWidth', 7)
      .attr('markerHeight', 7)
      .attr('orient', 'auto')
      .append('path')
      .attr('d', 'M0,-5L10,0L0,5')
      .attr('fill', '#6366f1');

    defs.append('marker')
      .attr('id', 'arrowhead-highlight')
      .attr('viewBox', '0 -5 10 10')
      .attr('refX', 28)
      .attr('refY', 0)
      .attr('markerWidth', 8)
      .attr('markerHeight', 8)
      .attr('orient', 'auto')
      .append('path')
      .attr('d', 'M0,-5L10,0L0,5')
      .attr('fill', '#38bdf8');

    // Deep background glow filter
    const filter = defs.append('filter')
      .attr('id', 'glow')
      .attr('x', '-20%')
      .attr('y', '-20%')
      .attr('width', '140%')
      .attr('height', '140%');
    filter.append('feGaussianBlur')
      .attr('stdDeviation', '4')
      .attr('result', 'blur');
    filter.append('feComposite')
      .attr('in', 'SourceGraphic')
      .attr('in2', 'blur')
      .attr('operator', 'over');

    // Root Group with Zoom
    const g = svg.append('g').attr('class', 'graph-root');

    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.3, 3])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
      });

    svg.call(zoom);
    zoomBehaviorRef.current = zoom;

    // Clone data for simulation
    const simNodes: GraphNode[] = filteredNodes.map(d => ({ ...d }));
    const simLinks: GraphLink[] = filteredLinks.map(d => ({
      ...d,
      source: typeof d.source === 'object' ? (d.source as GraphNode).id : d.source,
      target: typeof d.target === 'object' ? (d.target as GraphNode).id : d.target
    }));

    if (layoutMode === 'hierarchical') {
      // 3-Column Zanzibar Architecture Layout
      const col1X = width * 0.18;
      const col2X = width * 0.50;
      const col3X = width * 0.82;

      const subjects = simNodes.filter(n => n.category === 'subject');
      const roles = simNodes.filter(n => n.category === 'role');
      const resources = simNodes.filter(n => n.category === 'resource');

      const positionColumn = (list: GraphNode[], colX: number) => {
        const count = list.length;
        const startY = height * 0.16;
        const availableHeight = height * 0.72;
        const step = count > 1 ? availableHeight / (count - 1) : availableHeight / 2;

        list.forEach((n, idx) => {
          n.fx = colX;
          n.fy = count === 1 ? height / 2 : startY + idx * step;
          n.x = n.fx;
          n.y = n.fy;
        });
      };

      positionColumn(subjects, col1X);
      positionColumn(roles, col2X);
      positionColumn(resources, col3X);

      // Draw column boundary indicators
      const colLabels = [
        { label: 'PODMIOTY (SUBJECTS)', x: col1X, color: '#60a5fa' },
        { label: 'ROLE & GRUPY (INTERMEDIATE)', x: col2X, color: '#a78bfa' },
        { label: 'ZASOBY (OBJECTS)', x: col3X, color: '#34d399' }
      ];

      colLabels.forEach(col => {
        g.append('line')
          .attr('x1', col.x)
          .attr('y1', 30)
          .attr('x2', col.x)
          .attr('y2', height - 30)
          .attr('stroke', 'rgba(148, 163, 184, 0.1)')
          .attr('stroke-dasharray', '4,4');

        g.append('text')
          .attr('x', col.x)
          .attr('y', 24)
          .attr('text-anchor', 'middle')
          .attr('fill', col.color)
          .attr('font-size', '11px')
          .attr('font-weight', '600')
          .attr('letter-spacing', '0.05em')
          .text(col.label);
      });
    }

    // Initialize Force Simulation
    const simulation = d3.forceSimulation<GraphNode>(simNodes)
      .force('link', d3.forceLink<GraphNode, GraphLink>(simLinks)
        .id(d => d.id)
        .distance(layoutMode === 'hierarchical' ? 180 : 130)
      )
      .force('charge', d3.forceManyBody().strength(layoutMode === 'hierarchical' ? -150 : -420))
      .force('collision', d3.forceCollide().radius(48))
      .force('center', d3.forceCenter(width / 2, height / 2));

    if (layoutMode === 'hierarchical') {
      simulation.alpha(0.3).restart();
    }

    // Links container
    const linkGroup = g.append('g').attr('class', 'links');
    const linkPaths = linkGroup.selectAll('.link-path')
      .data(simLinks)
      .enter()
      .append('path')
      .attr('class', 'link-path')
      .attr('fill', 'none')
      .attr('stroke', '#4f46e5')
      .attr('stroke-width', 2)
      .attr('stroke-opacity', 0.6)
      .attr('marker-end', 'url(#arrowhead-default)')
      .style('cursor', 'pointer');

    // Link label badges along the middle of links
    const linkLabelsGroup = g.append('g').attr('class', 'link-labels');
    const linkLabels = linkLabelsGroup.selectAll('.link-label-group')
      .data(simLinks)
      .enter()
      .append('g')
      .attr('class', 'link-label-group')
      .style('cursor', 'pointer')
      .on('mouseenter', (event, d) => {
        setHoveredLink(d);
      })
      .on('mouseleave', () => {
        setHoveredLink(null);
      })
      .on('click', (event, d) => {
        const s = typeof d.source === 'object' ? (d.source as GraphNode).id : d.source;
        const t = typeof d.target === 'object' ? (d.target as GraphNode).id : d.target;
        if (onSelectForCheck) {
          onSelectForCheck(s, d.relation, t);
        }
      });

    linkLabels.append('rect')
      .attr('rx', 6)
      .attr('ry', 6)
      .attr('height', 18)
      .attr('fill', '#1e1b4b')
      .attr('stroke', '#6366f1')
      .attr('stroke-width', 1)
      .attr('opacity', 0.95);

    linkLabels.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 12)
      .attr('font-size', '10px')
      .attr('font-family', 'monospace')
      .attr('font-weight', '600')
      .attr('fill', '#c7d2fe')
      .text(d => d.relation);

    // Adjust label background rect widths based on text
    linkLabels.selectAll<SVGRectElement, GraphLink>('rect')
      .attr('width', function() {
        const parent = this.parentNode as SVGGElement;
        const textElem = parent.querySelector('text');
        const textWidth = textElem ? textElem.getComputedTextLength() : 40;
        return textWidth + 14;
      })
      .attr('x', function() {
        const parent = this.parentNode as SVGGElement;
        const textElem = parent.querySelector('text');
        const textWidth = textElem ? textElem.getComputedTextLength() : 40;
        return -(textWidth + 14) / 2;
      });

    // Nodes container
    const nodeGroup = g.append('g').attr('class', 'nodes');
    const nodeElements = nodeGroup.selectAll('.node-element')
      .data(simNodes)
      .enter()
      .append('g')
      .attr('class', 'node-element')
      .style('cursor', 'pointer')
      .call(
        d3.drag<SVGGElement, GraphNode>()
          .on('start', (event, d) => {
            if (!event.active) simulation.alphaTarget(0.2).restart();
            d.fx = d.x;
            d.fy = d.y;
          })
          .on('drag', (event, d) => {
            d.fx = event.x;
            d.fy = event.y;
          })
          .on('end', (event, d) => {
            if (!event.active) simulation.alphaTarget(0);
            if (layoutMode !== 'hierarchical') {
              d.fx = null;
              d.fy = null;
            }
          })
      )
      .on('click', (event, d) => {
        setSelectedNode(d);
      })
      .on('mouseenter', (event, d) => {
        setHoveredNode(d);
      })
      .on('mouseleave', () => {
        setHoveredNode(null);
      });

    // Outer glow circle for selected or hovered nodes
    nodeElements.append('circle')
      .attr('r', 28)
      .attr('fill', d => getNodeColor(d).bg)
      .attr('stroke', d => getNodeColor(d).stroke)
      .attr('stroke-width', 1.5)
      .attr('stroke-dasharray', d => d.type === 'service' ? '3,3' : 'none');

    // Inner core circle
    nodeElements.append('circle')
      .attr('r', 20)
      .attr('fill', '#0f172a')
      .attr('stroke', d => getNodeColor(d).fill)
      .attr('stroke-width', 2);

    // Node icon / symbol
    nodeElements.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 5)
      .attr('font-size', '11px')
      .attr('font-family', 'sans-serif')
      .attr('font-weight', 'bold')
      .attr('fill', d => getNodeColor(d).text)
      .text(d => {
        if (d.type === 'user') return '👤';
        if (d.type === 'role') return '🛡️';
        if (d.type === 'group') return '👥';
        if (d.type === 'service') return '⚡';
        return '📦';
      });

    // Node label badge below
    const labels = nodeElements.append('g')
      .attr('transform', 'translate(0, 34)');

    labels.append('rect')
      .attr('rx', 4)
      .attr('ry', 4)
      .attr('height', 16)
      .attr('fill', '#020617')
      .attr('stroke', '#334155')
      .attr('stroke-width', 1)
      .attr('opacity', 0.9);

    labels.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 11)
      .attr('font-size', '10px')
      .attr('font-family', 'monospace')
      .attr('fill', '#f1f5f9')
      .text(d => d.id);

    // Size label background
    labels.selectAll<SVGRectElement, GraphNode>('rect')
      .attr('width', function() {
        const parent = this.parentNode as SVGGElement;
        const textElem = parent.querySelector('text');
        const textWidth = textElem ? textElem.getComputedTextLength() : 40;
        return textWidth + 10;
      })
      .attr('x', function() {
        const parent = this.parentNode as SVGGElement;
        const textElem = parent.querySelector('text');
        const textWidth = textElem ? textElem.getComputedTextLength() : 40;
        return -(textWidth + 10) / 2;
      });

    // Simulation tick callback
    simulation.on('tick', () => {
      // Update link curves
      linkPaths.attr('d', (d: any) => {
        const sx = d.source.x;
        const sy = d.source.y;
        const tx = d.target.x;
        const ty = d.target.y;

        if (layoutMode === 'hierarchical') {
          // Smooth horizontal bezier curve
          const dx = tx - sx;
          return `M${sx},${sy}C${sx + dx * 0.45},${sy} ${tx - dx * 0.45},${ty} ${tx},${ty}`;
        } else {
          // Direct or slightly curved line
          const dx = tx - sx;
          const dy = ty - sy;
          const dr = Math.sqrt(dx * dx + dy * dy) * 1.5;
          return `M${sx},${sy}A${dr},${dr} 0 0,1 ${tx},${ty}`;
        }
      });

      // Update link labels position (midpoint)
      linkLabels.attr('transform', (d: any) => {
        const sx = d.source.x;
        const sy = d.source.y;
        const tx = d.target.x;
        const ty = d.target.y;
        const mx = (sx + tx) / 2;
        const my = (sy + ty) / 2;
        return `translate(${mx}, ${my})`;
      });

      // Update node positions
      nodeElements.attr('transform', (d) => `translate(${d.x}, ${d.y})`);
    });

    return () => {
      simulation.stop();
    };
  }, [filteredNodes, filteredLinks, layoutMode, isFullscreen]);

  // Handle Search and Selection Visuals
  useEffect(() => {
    if (!svgRef.current) return;
    const svg = d3.select(svgRef.current);

    const activeId = selectedNode?.id || (searchQuery.trim() ? searchQuery.trim().toLowerCase() : null);

    svg.selectAll('.node-element circle:first-child')
      .attr('stroke-width', function(d: any) {
        if (!activeId) return 1.5;
        if (d.id === activeId || d.id.toLowerCase().includes(activeId)) return 4;
        return 1;
      })
      .attr('stroke', function(d: any) {
        if (!activeId) return getNodeColor(d).stroke;
        if (d.id === activeId || d.id.toLowerCase().includes(activeId)) return '#38bdf8';
        return getNodeColor(d).stroke;
      });

    svg.selectAll('.link-path')
      .attr('stroke-width', function(d: any) {
        const sId = typeof d.source === 'object' ? d.source.id : d.source;
        const tId = typeof d.target === 'object' ? d.target.id : d.target;
        if (selectedNode && (sId === selectedNode.id || tId === selectedNode.id)) {
          return 3.5;
        }
        return 2;
      })
      .attr('stroke', function(d: any) {
        const sId = typeof d.source === 'object' ? d.source.id : d.source;
        const tId = typeof d.target === 'object' ? d.target.id : d.target;
        if (selectedNode && (sId === selectedNode.id || tId === selectedNode.id)) {
          return '#38bdf8';
        }
        return '#4f46e5';
      })
      .attr('stroke-opacity', function(d: any) {
        if (!selectedNode) return 0.6;
        const sId = typeof d.source === 'object' ? d.source.id : d.source;
        const tId = typeof d.target === 'object' ? d.target.id : d.target;
        return (sId === selectedNode.id || tId === selectedNode.id) ? 1 : 0.15;
      });
  }, [selectedNode, searchQuery]);

  // Zoom control helpers
  const handleZoomIn = () => {
    if (svgRef.current && zoomBehaviorRef.current) {
      d3.select(svgRef.current).transition().duration(300).call(zoomBehaviorRef.current.scaleBy, 1.3);
    }
  };

  const handleZoomOut = () => {
    if (svgRef.current && zoomBehaviorRef.current) {
      d3.select(svgRef.current).transition().duration(300).call(zoomBehaviorRef.current.scaleBy, 0.77);
    }
  };

  const handleResetZoom = () => {
    if (svgRef.current && zoomBehaviorRef.current) {
      d3.select(svgRef.current).transition().duration(400).call(zoomBehaviorRef.current.transform, d3.zoomIdentity);
      setSelectedNode(null);
    }
  };

  // Find connections of selected node
  const selectedNodeDetails = useMemo(() => {
    if (!selectedNode) return null;

    const inbound = links.filter(l => {
      const tId = typeof l.target === 'object' ? (l.target as GraphNode).id : l.target;
      return tId === selectedNode.id;
    });

    const outbound = links.filter(l => {
      const sId = typeof l.source === 'object' ? (l.source as GraphNode).id : l.source;
      return sId === selectedNode.id;
    });

    return {
      node: selectedNode,
      inbound,
      outbound,
      totalTuples: inbound.length + outbound.length
    };
  }, [selectedNode, links]);

  return (
    <div className={`flex flex-col bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl transition-all ${
      isFullscreen ? 'fixed inset-4 z-50 rounded-2xl border-indigo-500/50' : ''
    }`}>
      {/* Top Header & Toolbar */}
      <div className="px-5 py-3.5 bg-slate-950/80 border-b border-slate-800/80 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-indigo-500/20 text-indigo-400 rounded-lg border border-indigo-500/30">
            <Network className="w-5 h-5" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="text-base font-bold text-white tracking-wide">
                Graf modelu autoryzacji OpenFGA
              </h3>
              <span className="px-2 py-0.5 text-[10px] font-semibold bg-indigo-500/20 text-indigo-300 rounded border border-indigo-500/30 font-mono">
                Google Zanzibar ReBAC
              </span>
            </div>
            <p className="text-xs text-slate-400">
              Interaktywna mapa relacji łącząca <strong>użytkowników</strong>, <strong>role</strong> i <strong>zasoby</strong> na podstawie krotek uprawnień.
            </p>
          </div>
        </div>

        {/* Action Controls & Layout Switches */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Search */}
          <div className="relative">
            <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Filtruj węzeł..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              className="pl-8 pr-3 py-1.5 bg-slate-900 border border-slate-700/80 rounded-lg text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 w-36 sm:w-44 font-mono"
            />
          </div>

          {/* Layout Mode Toggle */}
          <div className="flex items-center bg-slate-900 p-1 rounded-lg border border-slate-800 text-xs">
            <button
              onClick={() => setLayoutMode('force')}
              className={`px-2.5 py-1 rounded font-medium transition cursor-pointer flex items-center gap-1.5 ${
                layoutMode === 'force' 
                  ? 'bg-indigo-600 text-white shadow-sm' 
                  : 'text-slate-400 hover:text-slate-200'
              }`}
              title="Graf siłowy (Force-Directed Physics)"
            >
              <Share2 className="w-3.5 h-3.5" />
              <span>Siłowy</span>
            </button>
            <button
              onClick={() => setLayoutMode('hierarchical')}
              className={`px-2.5 py-1 rounded font-medium transition cursor-pointer flex items-center gap-1.5 ${
                layoutMode === 'hierarchical' 
                  ? 'bg-indigo-600 text-white shadow-sm' 
                  : 'text-slate-400 hover:text-slate-200'
              }`}
              title="Kolumny Zanzibar (Podmioty -> Role -> Zasoby)"
            >
              <Layers className="w-3.5 h-3.5" />
              <span>Zanzibar 3-Kolumny</span>
            </button>
          </div>

          {/* Type Filter */}
          <div className="flex items-center bg-slate-900 p-1 rounded-lg border border-slate-800 text-xs">
            <Filter className="w-3 h-3 text-slate-500 ml-1.5 mr-1" />
            <select
              value={filterType}
              onChange={e => setFilterType(e.target.value as any)}
              className="bg-transparent text-slate-300 text-xs py-1 pr-2 focus:outline-none cursor-pointer"
            >
              <option value="all" className="bg-slate-900 text-white">Wszystkie węzły ({stats.totalNodes})</option>
              <option value="users" className="bg-slate-900 text-white">Użytkownicy ({stats.subjectsCount})</option>
              <option value="roles" className="bg-slate-900 text-white">Role & Grupy ({stats.rolesCount})</option>
              <option value="resources" className="bg-slate-900 text-white">Zasoby ({stats.resourcesCount})</option>
            </select>
          </div>

          {/* Zoom Buttons */}
          <div className="flex items-center bg-slate-900 rounded-lg border border-slate-800">
            <button
              onClick={handleZoomIn}
              className="p-1.5 text-slate-400 hover:text-white transition cursor-pointer"
              title="Przybliż (Zoom In)"
            >
              <ZoomIn className="w-4 h-4" />
            </button>
            <button
              onClick={handleZoomOut}
              className="p-1.5 text-slate-400 hover:text-white transition border-l border-slate-800 cursor-pointer"
              title="Oddal (Zoom Out)"
            >
              <ZoomOut className="w-4 h-4" />
            </button>
            <button
              onClick={handleResetZoom}
              className="p-1.5 text-slate-400 hover:text-white transition border-l border-slate-800 cursor-pointer"
              title="Resetuj widok"
            >
              <RotateCcw className="w-4 h-4" />
            </button>
          </div>

          {/* Fullscreen Toggle */}
          <button
            onClick={() => setIsFullscreen(!isFullscreen)}
            className="p-1.5 bg-slate-900 hover:bg-slate-800 text-slate-400 hover:text-white rounded-lg border border-slate-800 transition cursor-pointer"
            title={isFullscreen ? 'Wyjdź z pełnego ekranu' : 'Rozwiń na pełny ekran'}
          >
            {isFullscreen ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
          </button>
        </div>
      </div>

      {/* Main Canvas Area */}
      <div className="relative flex-1 min-h-[460px] bg-gradient-to-b from-slate-950 via-slate-900 to-slate-950" ref={containerRef}>
        {/* Subtle grid pattern background */}
        <div 
          className="absolute inset-0 opacity-[0.04] pointer-events-none"
          style={{
            backgroundImage: 'radial-gradient(#ffffff 1px, transparent 1px)',
            backgroundSize: '24px 24px'
          }}
        />

        {/* SVG Container */}
        <svg 
          ref={svgRef} 
          className="w-full h-full block cursor-grab active:cursor-grabbing"
        />

        {/* Interactive Overlay: Node Inspector Drawer */}
        {selectedNodeDetails && (
          <div className="absolute top-4 right-4 w-80 bg-slate-900/95 backdrop-blur-md border border-slate-700/80 rounded-xl p-4 shadow-2xl text-slate-200 z-10 animate-in fade-in slide-in-from-right-4 duration-200">
            <div className="flex items-start justify-between border-b border-slate-800 pb-2 mb-3">
              <div className="flex items-center gap-2">
                <span className="text-base">
                  {selectedNodeDetails.node.type === 'user' ? '👤' : 
                   selectedNodeDetails.node.type === 'role' ? '🛡️' : 
                   selectedNodeDetails.node.type === 'group' ? '👥' : '📦'}
                </span>
                <div>
                  <h4 className="font-mono font-bold text-sm text-white">
                    {selectedNodeDetails.node.id}
                  </h4>
                  <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-400 uppercase font-semibold">
                    {selectedNodeDetails.node.type}
                  </span>
                </div>
              </div>
              <button
                onClick={() => setSelectedNode(null)}
                className="text-slate-400 hover:text-white text-lg font-bold leading-none p-1 cursor-pointer"
              >
                &times;
              </button>
            </div>

            <div className="space-y-3 text-xs">
              {/* Outbound Relations */}
              <div>
                <span className="text-slate-400 font-semibold block mb-1">
                  Relacje wychodzące ({selectedNodeDetails.outbound.length}):
                </span>
                {selectedNodeDetails.outbound.length === 0 ? (
                  <span className="text-slate-500 italic">Brak relacji wychodzących</span>
                ) : (
                  <div className="space-y-1.5 max-h-36 overflow-y-auto pr-1">
                    {selectedNodeDetails.outbound.map(link => {
                      const t = typeof link.target === 'object' ? (link.target as GraphNode).id : link.target;
                      return (
                        <div 
                          key={link.id} 
                          className="p-2 rounded bg-slate-800/80 border border-slate-700 flex items-center justify-between gap-1 font-mono hover:border-indigo-500 transition cursor-pointer"
                          onClick={() => {
                            if (onSelectForCheck) {
                              onSelectForCheck(selectedNodeDetails.node.id, link.relation, t);
                            }
                          }}
                        >
                          <div className="flex items-center gap-1.5 truncate">
                            <span className="text-indigo-400 font-bold">#{link.relation}</span>
                            <ArrowRight className="w-3 h-3 text-slate-500 shrink-0" />
                            <span className="text-emerald-300 truncate" title={t}>{t}</span>
                          </div>
                          <Play className="w-3 h-3 text-indigo-400 shrink-0" title="Przetestuj w Check" />
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Inbound Relations */}
              <div>
                <span className="text-slate-400 font-semibold block mb-1">
                  Relacje wchodzące ({selectedNodeDetails.inbound.length}):
                </span>
                {selectedNodeDetails.inbound.length === 0 ? (
                  <span className="text-slate-500 italic">Brak relacji wchodzących</span>
                ) : (
                  <div className="space-y-1.5 max-h-36 overflow-y-auto pr-1">
                    {selectedNodeDetails.inbound.map(link => {
                      const s = typeof link.source === 'object' ? (link.source as GraphNode).id : link.source;
                      return (
                        <div 
                          key={link.id} 
                          className="p-2 rounded bg-slate-800/80 border border-slate-700 flex items-center justify-between gap-1 font-mono hover:border-indigo-500 transition cursor-pointer"
                          onClick={() => {
                            if (onSelectForCheck) {
                              onSelectForCheck(s, link.relation, selectedNodeDetails.node.id);
                            }
                          }}
                        >
                          <div className="flex items-center gap-1.5 truncate">
                            <span className="text-blue-300 truncate" title={s}>{s}</span>
                            <ArrowRight className="w-3 h-3 text-slate-500 shrink-0" />
                            <span className="text-indigo-400 font-bold">#{link.relation}</span>
                          </div>
                          <Play className="w-3 h-3 text-indigo-400 shrink-0" title="Przetestuj w Check" />
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Zanzibar tuple sample */}
              <div className="p-2 rounded bg-black/50 border border-slate-800 text-[11px] text-slate-400">
                <div className="text-indigo-400 font-semibold mb-0.5">Google Zanzibar Model:</div>
                <code>user_id: {selectedNodeDetails.node.id}</code>
              </div>
            </div>
          </div>
        )}

        {/* Bottom Legend & Metrics Banner */}
        <div className="absolute bottom-3 left-3 right-3 flex flex-wrap items-center justify-between gap-3 bg-slate-950/85 backdrop-blur-sm border border-slate-800/90 rounded-lg px-4 py-2 text-xs text-slate-300 pointer-events-auto">
          {/* Legend Items */}
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-blue-500 shadow-sm shadow-blue-500/50"></span>
              <span className="text-slate-300 font-medium">Podmioty / Użytkownicy ({stats.subjectsCount})</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-purple-500 shadow-sm shadow-purple-500/50"></span>
              <span className="text-slate-300 font-medium">Role & Grupy ({stats.rolesCount})</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-emerald-500 shadow-sm shadow-emerald-500/50"></span>
              <span className="text-slate-300 font-medium">Zasoby / Obiekty ({stats.resourcesCount})</span>
            </div>
            <div className="hidden sm:flex items-center gap-1.5 text-indigo-400">
              <ArrowRight className="w-3.5 h-3.5" />
              <span>Krotki relacji ({stats.tuplesCount})</span>
            </div>
          </div>

          {/* Quick Helper Tip */}
          <div className="flex items-center gap-2 text-slate-400">
            <Info className="w-3.5 h-3.5 text-indigo-400" />
            <span className="hidden md:inline">
              Kliknij węzeł lub etykietę relacji, aby sprawdzić krotkę w testerze Check.
            </span>
          </div>
        </div>
      </div>
    </div>
  );
};

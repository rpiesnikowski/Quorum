import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import * as d3 from 'd3';
import { 
  Network, 
  ZoomIn, 
  ZoomOut, 
  RotateCcw, 
  Layers, 
  Filter, 
  Shield, 
  Share2, 
  ArrowRight, 
  Info, 
  Play, 
  Maximize2, 
  Minimize2, 
  CheckCircle2, 
  Search, 
  Compass, 
  Download, 
  Route, 
  Sparkles, 
  Check, 
  CornerDownRight, 
  Radio, 
  Cpu,
  AlertCircle
} from 'lucide-react';
import { AuthZenRule } from './OpenFgaManagerTab';

export interface GraphNode extends d3.SimulationNodeDatum {
  id: string;
  label: string;
  type: 'user' | 'role' | 'group' | 'service' | 'resource';
  subType?: string;
  category: 'subject' | 'role' | 'resource';
  degree: number;
  inDegree: number;
  outDegree: number;
}

export interface GraphLink extends d3.SimulationLinkDatum<GraphNode> {
  id: string;
  source: string | GraphNode;
  target: string | GraphNode;
  relation: string;
  effect: 'Permit' | 'Deny';
  ruleName: string;
  ruleId: string;
  isTransitive?: boolean;
}

export interface TransitivePath {
  user: string;
  role: string;
  resource: string;
  userToRoleRelation: string;
  roleToResourceRelation: string;
  fullDescription: string;
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

  const [layoutMode, setLayoutMode] = useState<'hierarchical' | 'force' | 'radial'>('hierarchical');
  const [filterType, setFilterType] = useState<'all' | 'users' | 'roles' | 'resources'>('all');
  const [selectedNode, setSelectedNode] = useState<GraphNode | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [hoveredNode, setHoveredNode] = useState<GraphNode | null>(null);
  const [hoveredLink, setHoveredLink] = useState<GraphLink | null>(null);

  // Path Tracer state
  const [traceSubject, setTraceSubject] = useState<string>('');
  const [traceResource, setTraceResource] = useState<string>('');
  const [activeTracedPath, setActiveTracedPath] = useState<TransitivePath | null>(null);
  const [traceStatus, setTraceStatus] = useState<'idle' | 'found' | 'not_found'>('idle');

  // Transform active rules from policy store into nodes and links for Zanzibar ReBAC
  const { nodes, links, stats, transitivePaths } = useMemo(() => {
    const nodeMap = new Map<string, GraphNode>();
    const linkList: GraphLink[] = [];

    // Filter active Permit rules
    const activeRules = rules.filter(r => r.isEnabled && r.effect === 'Permit');

    activeRules.forEach(rule => {
      const subjectId = `${rule.subjectType}:${rule.subjectId}`;
      const isTargetRoleOrGroup = rule.resourceType === 'role' || rule.resourceType === 'group';
      const resourceId = `${rule.resourceType}:${rule.resourceId}`;

      // 1. Source subject node
      if (!nodeMap.has(subjectId)) {
        const isSubjectRoleOrGroup = rule.subjectType === 'role' || rule.subjectType === 'group';
        nodeMap.set(subjectId, {
          id: subjectId,
          label: rule.subjectId,
          type: rule.subjectType,
          category: isSubjectRoleOrGroup ? 'role' : 'subject',
          degree: 0,
          inDegree: 0,
          outDegree: 0
        });
      }

      // 2. Target resource/role node
      if (!nodeMap.has(resourceId)) {
        nodeMap.set(resourceId, {
          id: resourceId,
          label: rule.resourceId,
          type: isTargetRoleOrGroup ? (rule.resourceType as 'role' | 'group') : 'resource',
          subType: rule.resourceType,
          category: isTargetRoleOrGroup ? 'role' : 'resource',
          degree: 0,
          inDegree: 0,
          outDegree: 0
        });
      }

      // Increment degrees
      nodeMap.get(subjectId)!.degree += 1;
      nodeMap.get(subjectId)!.outDegree += 1;
      nodeMap.get(resourceId)!.degree += 1;
      nodeMap.get(resourceId)!.inDegree += 1;

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

    // Compute Transitive Paths (User -> Role -> Resource)
    const paths: TransitivePath[] = [];
    const subjects = allNodes.filter(n => n.category === 'subject');
    const roles = allNodes.filter(n => n.category === 'role');
    const resources = allNodes.filter(n => n.category === 'resource');

    subjects.forEach(sub => {
      // Find all roles this subject is member of
      const roleLinks = linkList.filter(l => {
        const s = typeof l.source === 'string' ? l.source : (l.source as GraphNode).id;
        const t = typeof l.target === 'string' ? l.target : (l.target as GraphNode).id;
        return s === sub.id && roles.some(r => r.id === t);
      });

      roleLinks.forEach(rl => {
        const roleId = typeof rl.target === 'string' ? rl.target : (rl.target as GraphNode).id;
        // Find all resources this role has permissions on
        const resLinks = linkList.filter(l => {
          const s = typeof l.source === 'string' ? l.source : (l.source as GraphNode).id;
          const t = typeof l.target === 'string' ? l.target : (l.target as GraphNode).id;
          return s === roleId && resources.some(res => res.id === t);
        });

        resLinks.forEach(resL => {
          const resId = typeof resL.target === 'string' ? resL.target : (resL.target as GraphNode).id;
          paths.push({
            user: sub.id,
            role: roleId,
            resource: resId,
            userToRoleRelation: rl.relation,
            roleToResourceRelation: resL.relation,
            fullDescription: `${sub.id} ➔ #${rl.relation} ➔ ${roleId} ➔ #${resL.relation} ➔ ${resId}`
          });
        });
      });
    });

    return {
      nodes: allNodes,
      links: linkList,
      stats: {
        totalNodes: allNodes.length,
        subjectsCount: allNodes.filter(n => n.category === 'subject').length,
        rolesCount: allNodes.filter(n => n.category === 'role').length,
        resourcesCount: allNodes.filter(n => n.category === 'resource').length,
        tuplesCount: linkList.length,
        transitiveCount: paths.length
      },
      transitivePaths: paths
    };
  }, [rules]);

  // Visual node filtering
  const filteredNodes = useMemo(() => {
    if (filterType === 'all') return nodes;
    if (filterType === 'users') return nodes.filter(n => n.category === 'subject');
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

  // Helper color map for tiers
  const getNodeColor = useCallback((node: GraphNode) => {
    if (node.category === 'subject') {
      return { 
        fill: '#3b82f6', 
        stroke: '#60a5fa', 
        glow: 'rgba(59, 130, 246, 0.4)', 
        text: '#93c5fd', 
        bg: 'rgba(59, 130, 246, 0.15)',
        badgeBg: 'bg-blue-500/20 text-blue-300 border-blue-500/30'
      };
    }
    if (node.category === 'role') {
      return { 
        fill: '#8b5cf6', 
        stroke: '#a78bfa', 
        glow: 'rgba(139, 92, 246, 0.4)', 
        text: '#c4b5fd', 
        bg: 'rgba(139, 92, 246, 0.15)',
        badgeBg: 'bg-purple-500/20 text-purple-300 border-purple-500/30'
      };
    }
    return { 
      fill: '#10b981', 
      stroke: '#34d399', 
      glow: 'rgba(16, 185, 129, 0.4)', 
      text: '#a7f3d0', 
      bg: 'rgba(16, 185, 129, 0.15)',
      badgeBg: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30'
    };
  }, []);

  // Path tracing handler
  const handleTracePath = () => {
    if (!traceSubject || !traceResource) return;

    // Check direct link
    const direct = links.find(l => {
      const s = typeof l.source === 'string' ? l.source : (l.source as GraphNode).id;
      const t = typeof l.target === 'string' ? l.target : (l.target as GraphNode).id;
      return s === traceSubject && t === traceResource;
    });

    if (direct) {
      setActiveTracedPath({
        user: traceSubject,
        role: '(Dostęp bezpośredni)',
        resource: traceResource,
        userToRoleRelation: direct.relation,
        roleToResourceRelation: direct.relation,
        fullDescription: `${traceSubject} ➔ #${direct.relation} ➔ ${traceResource} (Bezpośrednie przypisanie)`
      });
      setTraceStatus('found');
      return;
    }

    // Check transitive path through roles
    const path = transitivePaths.find(p => p.user === traceSubject && p.resource === traceResource);
    if (path) {
      setActiveTracedPath(path);
      setTraceStatus('found');
    } else {
      setActiveTracedPath(null);
      setTraceStatus('not_found');
    }
  };

  const handleClearTrace = () => {
    setTraceSubject('');
    setTraceResource('');
    setActiveTracedPath(null);
    setTraceStatus('idle');
  };

  // Render D3 Graph
  useEffect(() => {
    if (!svgRef.current || !containerRef.current) return;

    const width = containerRef.current.clientWidth || 900;
    const height = isFullscreen ? window.innerHeight - 170 : 540;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    svg
      .attr('width', width)
      .attr('height', height)
      .attr('viewBox', [0, 0, width, height]);

    // Defs for arrowhead markers and glowing filters
    const defs = svg.append('defs');

    // Arrow markers
    const createMarker = (id: string, color: string, refX = 28) => {
      defs.append('marker')
        .attr('id', id)
        .attr('viewBox', '0 -5 10 10')
        .attr('refX', refX)
        .attr('refY', 0)
        .attr('markerWidth', 7)
        .attr('markerHeight', 7)
        .attr('orient', 'auto')
        .append('path')
        .attr('d', 'M0,-5L10,0L0,5')
        .attr('fill', color);
    };

    createMarker('arrowhead-default', '#6366f1');
    createMarker('arrowhead-highlight', '#38bdf8', 30);
    createMarker('arrowhead-traced', '#f59e0b', 30);

    // Deep background glow filter
    const filter = defs.append('filter')
      .attr('id', 'glow')
      .attr('x', '-30%')
      .attr('y', '-30%')
      .attr('width', '160%')
      .attr('height', '160%');
    filter.append('feGaussianBlur')
      .attr('stdDeviation', '5')
      .attr('result', 'blur');
    filter.append('feComposite')
      .attr('in', 'SourceGraphic')
      .attr('in2', 'blur')
      .attr('operator', 'over');

    // Root Group with Zoom
    const g = svg.append('g').attr('class', 'graph-root');

    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.25, 3.5])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
      });

    svg.call(zoom);
    zoomBehaviorRef.current = zoom;

    // Clone data for layout
    const simNodes: GraphNode[] = filteredNodes.map(d => ({ ...d }));
    const simLinks: GraphLink[] = filteredLinks.map(d => ({
      ...d,
      source: typeof d.source === 'object' ? (d.source as GraphNode).id : d.source,
      target: typeof d.target === 'object' ? (d.target as GraphNode).id : d.target
    }));

    // -------------------------------------------------------------
    // Layout 1: Zanzibar 3-Column Flow (Users ➔ Roles ➔ Resources)
    // -------------------------------------------------------------
    if (layoutMode === 'hierarchical') {
      const col1X = width * 0.16;
      const col2X = width * 0.50;
      const col3X = width * 0.84;

      const subjects = simNodes.filter(n => n.category === 'subject');
      const roles = simNodes.filter(n => n.category === 'role');
      const resources = simNodes.filter(n => n.category === 'resource');

      const positionColumn = (list: GraphNode[], colX: number) => {
        const count = list.length;
        const startY = height * 0.16;
        const availableHeight = height * 0.72;
        const step = count > 1 ? availableHeight / (count - 1) : 0;

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

      // Draw column boundary guides
      const colLabels = [
        { label: 'PODMIOTY (USERS & SERVICES)', x: col1X, color: '#60a5fa', desc: 'Żądający dostępu' },
        { label: 'ROLE & GRUPY (INTERMEDIATE)', x: col2X, color: '#a78bfa', desc: 'Przypisania uprawnień' },
        { label: 'ZASOBY (RESOURCES & OBJECTS)', x: col3X, color: '#34d399', desc: 'Obiekty chronione' }
      ];

      colLabels.forEach(col => {
        // Vertical dashed column guide
        g.append('line')
          .attr('x1', col.x)
          .attr('y1', 28)
          .attr('x2', col.x)
          .attr('y2', height - 28)
          .attr('stroke', 'rgba(148, 163, 184, 0.12)')
          .attr('stroke-dasharray', '5,5');

        // Column Header
        const headerG = g.append('g').attr('transform', `translate(${col.x}, 24)`);
        
        headerG.append('text')
          .attr('text-anchor', 'middle')
          .attr('fill', col.color)
          .attr('font-size', '11px')
          .attr('font-weight', '700')
          .attr('letter-spacing', '0.06em')
          .text(col.label);

        headerG.append('text')
          .attr('text-anchor', 'middle')
          .attr('y', 14)
          .attr('fill', '#64748b')
          .attr('font-size', '9px')
          .text(col.desc);
      });
    } 
    // -------------------------------------------------------------
    // Layout 2: Radial Hub & Spoke (Resources Center, Roles Middle, Users Outer)
    // -------------------------------------------------------------
    else if (layoutMode === 'radial') {
      const centerX = width / 2;
      const centerY = height / 2;

      const subjects = simNodes.filter(n => n.category === 'subject');
      const roles = simNodes.filter(n => n.category === 'role');
      const resources = simNodes.filter(n => n.category === 'resource');

      const placeInCircle = (list: GraphNode[], radius: number) => {
        const count = list.length;
        list.forEach((n, idx) => {
          const angle = (idx / count) * 2 * Math.PI - Math.PI / 2;
          n.fx = centerX + radius * Math.cos(angle);
          n.fy = centerY + radius * Math.sin(angle);
          n.x = n.fx;
          n.y = n.fy;
        });
      };

      const maxR = Math.min(width, height) * 0.42;
      placeInCircle(resources, maxR * 0.32);
      placeInCircle(roles, maxR * 0.65);
      placeInCircle(subjects, maxR * 0.96);

      // Draw concentric tier rings
      [maxR * 0.32, maxR * 0.65, maxR * 0.96].forEach((r, idx) => {
        g.append('circle')
          .attr('cx', centerX)
          .attr('cy', centerY)
          .attr('r', r)
          .attr('fill', 'none')
          .attr('stroke', 'rgba(148, 163, 184, 0.08)')
          .attr('stroke-dasharray', '4,4');
      });
    }

    // -------------------------------------------------------------
    // Force Simulation Setup
    // -------------------------------------------------------------
    const simulation = d3.forceSimulation<GraphNode>(simNodes)
      .force('link', d3.forceLink<GraphNode, GraphLink>(simLinks)
        .id(d => d.id)
        .distance(layoutMode === 'force' ? 140 : 200)
      )
      .force('charge', d3.forceManyBody().strength(layoutMode === 'force' ? -460 : -100))
      .force('collision', d3.forceCollide().radius(48))
      .force('center', d3.forceCenter(width / 2, height / 2));

    if (layoutMode !== 'force') {
      simulation.alpha(0.15).restart();
    }

    // -------------------------------------------------------------
    // Draw Links
    // -------------------------------------------------------------
    const linkGroup = g.append('g').attr('class', 'links');
    const linkPaths = linkGroup.selectAll('.link-path')
      .data(simLinks)
      .enter()
      .append('path')
      .attr('class', 'link-path')
      .attr('fill', 'none')
      .attr('stroke', '#4f46e5')
      .attr('stroke-width', 2)
      .attr('stroke-opacity', 0.65)
      .attr('marker-end', 'url(#arrowhead-default)')
      .style('cursor', 'pointer');

    // Link label badges along middle of link curves
    const linkLabelsGroup = g.append('g').attr('class', 'link-labels');
    const linkLabels = linkLabelsGroup.selectAll('.link-label-group')
      .data(simLinks)
      .enter()
      .append('g')
      .attr('class', 'link-label-group')
      .style('cursor', 'pointer')
      .on('mouseenter', (_, d) => setHoveredLink(d))
      .on('mouseleave', () => setHoveredLink(null))
      .on('click', (_, d) => {
        const s = typeof d.source === 'object' ? (d.source as GraphNode).id : d.source;
        const t = typeof d.target === 'object' ? (d.target as GraphNode).id : d.target;
        if (onSelectForCheck) {
          onSelectForCheck(s, d.relation, t);
        }
      });

    linkLabels.append('rect')
      .attr('rx', 5)
      .attr('ry', 5)
      .attr('height', 17)
      .attr('fill', '#111827')
      .attr('stroke', '#4f46e5')
      .attr('stroke-width', 1)
      .attr('opacity', 0.94);

    linkLabels.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 12)
      .attr('font-size', '9.5px')
      .attr('font-family', 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace')
      .attr('font-weight', '600')
      .attr('fill', '#c7d2fe')
      .text(d => `#${d.relation}`);

    // Compute dynamic width for label background rect
    linkLabels.selectAll<SVGRectElement, GraphLink>('rect')
      .attr('width', function() {
        const parent = this.parentNode as SVGGElement;
        const textElem = parent.querySelector('text');
        const textWidth = textElem ? textElem.getComputedTextLength() : 40;
        return textWidth + 12;
      })
      .attr('x', function() {
        const parent = this.parentNode as SVGGElement;
        const textElem = parent.querySelector('text');
        const textWidth = textElem ? textElem.getComputedTextLength() : 40;
        return -(textWidth + 12) / 2;
      });

    // -------------------------------------------------------------
    // Draw Nodes
    // -------------------------------------------------------------
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
            if (layoutMode === 'force') {
              d.fx = null;
              d.fy = null;
            }
          })
      )
      .on('click', (_, d) => {
        setSelectedNode(d);
      })
      .on('mouseenter', (_, d) => setHoveredNode(d))
      .on('mouseleave', () => setHoveredNode(null));

    // Outer glow halo circle
    nodeElements.append('circle')
      .attr('class', 'node-halo')
      .attr('r', 27)
      .attr('fill', d => getNodeColor(d).bg)
      .attr('stroke', d => getNodeColor(d).stroke)
      .attr('stroke-width', 1.5)
      .attr('stroke-dasharray', d => d.type === 'service' ? '3,3' : 'none');

    // Inner core circle
    nodeElements.append('circle')
      .attr('class', 'node-core')
      .attr('r', 19)
      .attr('fill', '#090d16')
      .attr('stroke', d => getNodeColor(d).fill)
      .attr('stroke-width', 2);

    // Node icon / symbol
    nodeElements.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 5)
      .attr('font-size', '12px')
      .attr('fill', d => getNodeColor(d).text)
      .text(d => {
        if (d.type === 'user') return '👤';
        if (d.type === 'role') return '🛡️';
        if (d.type === 'group') return '👥';
        if (d.type === 'service') return '⚡';
        return '📦';
      });

    // Degree counter badge on top-right of node
    const badgeGroup = nodeElements.append('g')
      .attr('transform', 'translate(14, -14)');

    badgeGroup.append('circle')
      .attr('r', 8)
      .attr('fill', '#1e1b4b')
      .attr('stroke', '#6366f1')
      .attr('stroke-width', 1);

    badgeGroup.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 3)
      .attr('font-size', '8px')
      .attr('font-family', 'monospace')
      .attr('font-weight', 'bold')
      .attr('fill', '#c7d2fe')
      .text(d => d.degree);

    // Node label badge below
    const labels = nodeElements.append('g')
      .attr('class', 'node-label-group')
      .attr('transform', 'translate(0, 33)');

    labels.append('rect')
      .attr('rx', 4)
      .attr('ry', 4)
      .attr('height', 16)
      .attr('fill', '#020617')
      .attr('stroke', '#334155')
      .attr('stroke-width', 1)
      .attr('opacity', 0.92);

    labels.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 11)
      .attr('font-size', '10px')
      .attr('font-family', 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace')
      .attr('fill', '#f1f5f9')
      .text(d => d.id);

    // Auto-size label rects
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

    // -------------------------------------------------------------
    // Simulation Tick Callback
    // -------------------------------------------------------------
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
          return `M${sx},${sy}C${sx + dx * 0.48},${sy} ${tx - dx * 0.48},${ty} ${tx},${ty}`;
        } else {
          // Slightly curved arc
          const dx = tx - sx;
          const dy = ty - sy;
          const dr = Math.sqrt(dx * dx + dy * dy) * 1.4;
          return `M${sx},${sy}A${dr},${dr} 0 0,1 ${tx},${ty}`;
        }
      });

      // Update link labels midpoint position
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
      nodeElements.attr('transform', d => `translate(${d.x}, ${d.y})`);
    });

    return () => {
      simulation.stop();
    };
  }, [filteredNodes, filteredLinks, layoutMode, isFullscreen, getNodeColor, onSelectForCheck]);

  // -------------------------------------------------------------
  // Visual Highlighting for Selection, Hover & Traced Path
  // -------------------------------------------------------------
  useEffect(() => {
    if (!svgRef.current) return;
    const svg = d3.select(svgRef.current);

    const activeId = selectedNode?.id || (searchQuery.trim() ? searchQuery.trim().toLowerCase() : null);

    // Nodes in traced path
    const tracedNodeIds = new Set<string>();
    if (activeTracedPath) {
      tracedNodeIds.add(activeTracedPath.user);
      if (activeTracedPath.role && activeTracedPath.role !== '(Dostęp bezpośredni)') {
        tracedNodeIds.add(activeTracedPath.role);
      }
      tracedNodeIds.add(activeTracedPath.resource);
    }

    svg.selectAll('.node-element').each(function(d: any) {
      const el = d3.select(this);
      const isTraced = tracedNodeIds.has(d.id);
      const isSelected = activeId && (d.id === activeId || d.id.toLowerCase().includes(activeId));

      el.select('.node-halo')
        .attr('stroke-width', isTraced || isSelected ? 3.5 : 1.5)
        .attr('stroke', isTraced ? '#f59e0b' : isSelected ? '#38bdf8' : getNodeColor(d).stroke);

      el.select('.node-core')
        .attr('fill', isTraced ? '#451a03' : isSelected ? '#082f49' : '#090d16');

      // Opacity dimming if filtering by active path or selection
      if (activeTracedPath) {
        el.attr('opacity', isTraced ? 1 : 0.22);
      } else if (selectedNode) {
        // Connected to selectedNode?
        const isConnected = links.some(l => {
          const s = typeof l.source === 'object' ? (l.source as GraphNode).id : l.source;
          const t = typeof l.target === 'object' ? (l.target as GraphNode).id : l.target;
          return (s === selectedNode.id && t === d.id) || (t === selectedNode.id && s === d.id);
        }) || d.id === selectedNode.id;

        el.attr('opacity', isConnected ? 1 : 0.22);
      } else {
        el.attr('opacity', 1);
      }
    });

    // Links highlighting
    svg.selectAll('.link-path').each(function(d: any) {
      const el = d3.select(this);
      const sId = typeof d.source === 'object' ? d.source.id : d.source;
      const tId = typeof d.target === 'object' ? d.target.id : d.target;

      let isTracedLink = false;
      if (activeTracedPath) {
        if (activeTracedPath.role === '(Dostęp bezpośredni)') {
          isTracedLink = sId === activeTracedPath.user && tId === activeTracedPath.resource;
        } else {
          isTracedLink = (sId === activeTracedPath.user && tId === activeTracedPath.role) ||
                         (sId === activeTracedPath.role && tId === activeTracedPath.resource);
        }
      }

      const isConnectedToSelected = selectedNode && (sId === selectedNode.id || tId === selectedNode.id);

      if (isTracedLink) {
        el
          .attr('stroke', '#f59e0b')
          .attr('stroke-width', 3.5)
          .attr('stroke-opacity', 1)
          .attr('marker-end', 'url(#arrowhead-traced)');
      } else if (isConnectedToSelected) {
        el
          .attr('stroke', '#38bdf8')
          .attr('stroke-width', 3)
          .attr('stroke-opacity', 1)
          .attr('marker-end', 'url(#arrowhead-highlight)');
      } else if (activeTracedPath || selectedNode) {
        el
          .attr('stroke', '#4f46e5')
          .attr('stroke-width', 1.5)
          .attr('stroke-opacity', 0.12)
          .attr('marker-end', 'url(#arrowhead-default)');
      } else {
        el
          .attr('stroke', '#4f46e5')
          .attr('stroke-width', 2)
          .attr('stroke-opacity', 0.65)
          .attr('marker-end', 'url(#arrowhead-default)');
      }
    });
  }, [selectedNode, searchQuery, activeTracedPath, getNodeColor, links]);

  // Zoom controls
  const handleZoomIn = () => {
    if (svgRef.current && zoomBehaviorRef.current) {
      d3.select(svgRef.current).transition().duration(250).call(zoomBehaviorRef.current.scaleBy, 1.3);
    }
  };

  const handleZoomOut = () => {
    if (svgRef.current && zoomBehaviorRef.current) {
      d3.select(svgRef.current).transition().duration(250).call(zoomBehaviorRef.current.scaleBy, 0.77);
    }
  };

  const handleResetZoom = () => {
    if (svgRef.current && zoomBehaviorRef.current) {
      d3.select(svgRef.current).transition().duration(350).call(zoomBehaviorRef.current.transform, d3.zoomIdentity);
      setSelectedNode(null);
      handleClearTrace();
    }
  };

  // Export SVG visual
  const handleExportSvg = () => {
    if (!svgRef.current) return;
    const svgString = new XMLSerializer().serializeToString(svgRef.current);
    const blob = new Blob([svgString], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `openfga-rebac-model-${new Date().toISOString().substring(0, 10)}.svg`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  // Inspect selected node relations
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

    // Transitive relations connected to this node
    const relatedTransitive = transitivePaths.filter(p => 
      p.user === selectedNode.id || p.role === selectedNode.id || p.resource === selectedNode.id
    );

    return {
      node: selectedNode,
      inbound,
      outbound,
      relatedTransitive,
      totalTuples: inbound.length + outbound.length
    };
  }, [selectedNode, links, transitivePaths]);

  // Unique lists for the Path Tracer selectors
  const allUserOptions = useMemo(() => nodes.filter(n => n.category === 'subject').map(n => n.id), [nodes]);
  const allResourceOptions = useMemo(() => nodes.filter(n => n.category === 'resource').map(n => n.id), [nodes]);

  return (
    <div className={`flex flex-col bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-2xl transition-all ${
      isFullscreen ? 'fixed inset-3 z-50 rounded-2xl border-indigo-500/50' : ''
    }`}>
      {/* Top Header & Interactive Control Bar */}
      <div className="px-5 py-3.5 bg-slate-950/90 border-b border-slate-800/90 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-indigo-500/20 text-indigo-400 rounded-lg border border-indigo-500/30 shadow-inner">
            <Network className="w-5 h-5" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="text-base font-bold text-white tracking-wide flex items-center gap-2">
                Graf Relacji Autoryzacji OpenFGA (ReBAC)
              </h3>
              <span className="px-2 py-0.5 text-[10px] font-semibold bg-indigo-500/20 text-indigo-300 rounded border border-indigo-500/30 font-mono">
                D3.js Dynamic Visualizer
              </span>
            </div>
            <p className="text-xs text-slate-400">
              Wizualizacja powiązań między <strong>użytkownikami</strong>, <strong>rolami</strong> i <strong>zasobami</strong> w czasie rzeczywistym z magazynu reguł.
            </p>
          </div>
        </div>

        {/* Layout & Control Buttons */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Quick Search */}
          <div className="relative">
            <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Szukaj węzła..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              className="pl-8 pr-3 py-1.5 bg-slate-900 border border-slate-700/80 rounded-lg text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 w-32 sm:w-40 font-mono"
            />
          </div>

          {/* Layout Mode Toggle */}
          <div className="flex items-center bg-slate-900 p-1 rounded-lg border border-slate-800 text-xs">
            <button
              onClick={() => setLayoutMode('hierarchical')}
              className={`px-2.5 py-1 rounded font-medium transition cursor-pointer flex items-center gap-1.5 ${
                layoutMode === 'hierarchical' 
                  ? 'bg-indigo-600 text-white shadow-sm' 
                  : 'text-slate-400 hover:text-slate-200'
              }`}
              title="Kolumny Zanzibar (Użytkownicy ➔ Role ➔ Zasoby)"
            >
              <Layers className="w-3.5 h-3.5" />
              <span>3 Kolumny</span>
            </button>
            <button
              onClick={() => setLayoutMode('force')}
              className={`px-2.5 py-1 rounded font-medium transition cursor-pointer flex items-center gap-1.5 ${
                layoutMode === 'force' 
                  ? 'bg-indigo-600 text-white shadow-sm' 
                  : 'text-slate-400 hover:text-slate-200'
              }`}
              title="Fizyka grafu siłowego (Force-Directed D3)"
            >
              <Share2 className="w-3.5 h-3.5" />
              <span>Siłowy</span>
            </button>
            <button
              onClick={() => setLayoutMode('radial')}
              className={`px-2.5 py-1 rounded font-medium transition cursor-pointer flex items-center gap-1.5 ${
                layoutMode === 'radial' 
                  ? 'bg-indigo-600 text-white shadow-sm' 
                  : 'text-slate-400 hover:text-slate-200'
              }`}
              title="Układ koncentryczny (Radialny)"
            >
              <Compass className="w-3.5 h-3.5" />
              <span>Radialny</span>
            </button>
          </div>

          {/* Category Filter */}
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

          {/* Zoom & Reset Controls */}
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
              title="Resetuj skalę / Wyczyść śledzenie"
            >
              <RotateCcw className="w-4 h-4" />
            </button>
          </div>

          {/* Export SVG */}
          <button
            onClick={handleExportSvg}
            className="p-1.5 bg-slate-900 hover:bg-slate-800 text-slate-400 hover:text-white rounded-lg border border-slate-800 transition cursor-pointer"
            title="Eksportuj jako wektorowy plik SVG"
          >
            <Download className="w-4 h-4" />
          </button>

          {/* Fullscreen Toggle */}
          <button
            onClick={() => setIsFullscreen(!isFullscreen)}
            className="p-1.5 bg-slate-900 hover:bg-slate-800 text-slate-400 hover:text-white rounded-lg border border-slate-800 transition cursor-pointer"
            title={isFullscreen ? 'Zminimalizuj' : 'Pełny ekran'}
          >
            {isFullscreen ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
          </button>
        </div>
      </div>

      {/* Path Tracer Banner: Interactive Relationship Query Analyzer */}
      <div className="px-5 py-2.5 bg-indigo-950/40 border-b border-indigo-900/40 flex flex-wrap items-center justify-between gap-3 text-xs">
        <div className="flex items-center gap-2">
          <Sparkles className="w-4 h-4 text-amber-400 shrink-0" />
          <span className="font-semibold text-indigo-200">
            Analizator ścieżki autoryzacji (Transitive ReBAC Tracer):
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/* Subject selector */}
          <select
            value={traceSubject}
            onChange={e => setTraceSubject(e.target.value)}
            className="px-2.5 py-1 bg-slate-900/90 border border-indigo-500/40 rounded text-slate-200 text-xs focus:ring-1 focus:ring-indigo-400"
          >
            <option value="">-- Wybierz Użytkownika --</option>
            {allUserOptions.map(u => (
              <option key={u} value={u}>{u}</option>
            ))}
          </select>

          <ArrowRight className="w-3.5 h-3.5 text-indigo-400 shrink-0" />

          {/* Resource selector */}
          <select
            value={traceResource}
            onChange={e => setTraceResource(e.target.value)}
            className="px-2.5 py-1 bg-slate-900/90 border border-indigo-500/40 rounded text-slate-200 text-xs focus:ring-1 focus:ring-indigo-400"
          >
            <option value="">-- Wybierz Zasób --</option>
            {allResourceOptions.map(r => (
              <option key={r} value={r}>{r}</option>
            ))}
          </select>

          <button
            onClick={handleTracePath}
            disabled={!traceSubject || !traceResource}
            className="px-3 py-1 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white font-medium rounded text-xs transition cursor-pointer flex items-center gap-1.5 shadow"
          >
            <Radio className="w-3 h-3" />
            <span>Śledź relację</span>
          </button>

          {activeTracedPath && (
            <button
              onClick={handleClearTrace}
              className="px-2 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-xs transition cursor-pointer"
            >
              Wyczyść
            </button>
          )}
        </div>

        {traceStatus === 'found' && activeTracedPath && (
          <div className="w-full mt-1.5 p-2 bg-emerald-950/50 border border-emerald-500/40 rounded-lg text-emerald-300 flex items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
              <span>
                <strong>Ścieżka znaleziona: </strong>
                {activeTracedPath.fullDescription}
              </span>
            </div>
            {onSelectForCheck && (
              <button
                onClick={() => onSelectForCheck(activeTracedPath.user, activeTracedPath.roleToResourceRelation, activeTracedPath.resource)}
                className="px-2 py-0.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-[11px] font-medium transition cursor-pointer shrink-0"
              >
                Przetestuj w Check API
              </button>
            )}
          </div>
        )}

        {traceStatus === 'not_found' && (
          <div className="w-full mt-1.5 p-2 bg-rose-950/50 border border-rose-500/40 rounded-lg text-rose-300 flex items-center gap-2">
            <AlertCircle className="w-4 h-4 text-rose-400 shrink-0" />
            <span>
              Brak ścieżki autoryzacji między <code>{traceSubject}</code> a <code>{traceResource}</code> w obecnym magazynie polityk.
            </span>
          </div>
        )}
      </div>

      {/* Main Graph Canvas */}
      <div 
        className="relative flex-1 min-h-[480px] bg-gradient-to-b from-slate-950 via-slate-900 to-slate-950 select-none" 
        ref={containerRef}
      >
        {/* Subtle grid backdrop */}
        <div 
          className="absolute inset-0 opacity-[0.035] pointer-events-none"
          style={{
            backgroundImage: 'radial-gradient(#ffffff 1px, transparent 1px)',
            backgroundSize: '24px 24px'
          }}
        />

        {/* SVG Viewport */}
        <svg 
          ref={svgRef} 
          className="w-full h-full block cursor-grab active:cursor-grabbing"
        />

        {/* Selected Node Details Drawer */}
        {selectedNodeDetails && (
          <div className="absolute top-4 right-4 w-84 bg-slate-900/95 backdrop-blur-md border border-slate-700/80 rounded-xl p-4 shadow-2xl text-slate-200 z-10 animate-in fade-in slide-in-from-right-4 duration-200">
            <div className="flex items-start justify-between border-b border-slate-800 pb-2 mb-3">
              <div className="flex items-center gap-2">
                <span className="text-xl">
                  {selectedNodeDetails.node.type === 'user' ? '👤' : 
                   selectedNodeDetails.node.type === 'role' ? '🛡️' : 
                   selectedNodeDetails.node.type === 'group' ? '👥' : 
                   selectedNodeDetails.node.type === 'service' ? '⚡' : '📦'}
                </span>
                <div>
                  <h4 className="font-mono font-bold text-sm text-white truncate max-w-[190px]" title={selectedNodeDetails.node.id}>
                    {selectedNodeDetails.node.id}
                  </h4>
                  <div className="flex items-center gap-1.5 mt-0.5">
                    <span className={`text-[10px] px-1.5 py-0.2 rounded uppercase font-semibold border ${getNodeColor(selectedNodeDetails.node).badgeBg}`}>
                      {selectedNodeDetails.node.category} ({selectedNodeDetails.node.type})
                    </span>
                    <span className="text-[10px] text-slate-400">
                      Stopień: {selectedNodeDetails.totalTuples}
                    </span>
                  </div>
                </div>
              </div>
              <button
                onClick={() => setSelectedNode(null)}
                className="text-slate-400 hover:text-white text-lg font-bold leading-none p-1 cursor-pointer"
              >
                &times;
              </button>
            </div>

            <div className="space-y-3.5 text-xs max-h-[380px] overflow-y-auto pr-1">
              {/* Outbound Relations */}
              <div>
                <span className="text-slate-400 font-semibold block mb-1">
                  Relacje wychodzące ({selectedNodeDetails.outbound.length}):
                </span>
                {selectedNodeDetails.outbound.length === 0 ? (
                  <span className="text-slate-500 italic">Brak relacji wychodzących</span>
                ) : (
                  <div className="space-y-1.5">
                    {selectedNodeDetails.outbound.map(link => {
                      const t = typeof link.target === 'object' ? (link.target as GraphNode).id : link.target;
                      return (
                        <div 
                          key={link.id} 
                          className="p-2 rounded bg-slate-800/80 border border-slate-700 flex items-center justify-between gap-1 font-mono hover:border-indigo-500 transition cursor-pointer group"
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
                          <Play className="w-3 h-3 text-indigo-400 opacity-0 group-hover:opacity-100 transition shrink-0" title="Testuj Check" />
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
                  <div className="space-y-1.5">
                    {selectedNodeDetails.inbound.map(link => {
                      const s = typeof link.source === 'object' ? (link.source as GraphNode).id : link.source;
                      return (
                        <div 
                          key={link.id} 
                          className="p-2 rounded bg-slate-800/80 border border-slate-700 flex items-center justify-between gap-1 font-mono hover:border-indigo-500 transition cursor-pointer group"
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
                          <Play className="w-3 h-3 text-indigo-400 opacity-0 group-hover:opacity-100 transition shrink-0" title="Testuj Check" />
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Transitive Paths connected */}
              {selectedNodeDetails.relatedTransitive.length > 0 && (
                <div>
                  <span className="text-amber-400 font-semibold block mb-1">
                    Połączenia pośrednie ({selectedNodeDetails.relatedTransitive.length}):
                  </span>
                  <div className="space-y-1.5">
                    {selectedNodeDetails.relatedTransitive.map((p, idx) => (
                      <div key={idx} className="p-2 rounded bg-amber-950/30 border border-amber-500/30 text-[11px] font-mono text-amber-200">
                        <CornerDownRight className="w-3 h-3 inline mr-1 text-amber-400" />
                        {p.user} ➔ #{p.userToRoleRelation} ➔ {p.role} ➔ #{p.roleToResourceRelation} ➔ {p.resource}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Quick Zanzibar Tuple representation */}
              <div className="p-2.5 rounded bg-black/60 border border-slate-800 text-[11px] text-slate-400">
                <div className="text-indigo-400 font-semibold mb-1 flex items-center gap-1">
                  <Cpu className="w-3 h-3" />
                  <span>Krotka Google Zanzibar:</span>
                </div>
                <code className="text-[10.5px] text-slate-300 block">
                  node_id: &quot;{selectedNodeDetails.node.id}&quot;<br />
                  category: &quot;{selectedNodeDetails.node.category}&quot;
                </code>
              </div>
            </div>
          </div>
        )}

        {/* Bottom Legend & Live Metrics Bar */}
        <div className="absolute bottom-3 left-3 right-3 flex flex-wrap items-center justify-between gap-3 bg-slate-950/85 backdrop-blur-md border border-slate-800/90 rounded-lg px-4 py-2.5 text-xs text-slate-300 pointer-events-auto shadow-lg">
          {/* Legend Items */}
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-blue-500 shadow-sm shadow-blue-500/50"></span>
              <span className="text-slate-200 font-medium">Podmioty / Użytkownicy ({stats.subjectsCount})</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-purple-500 shadow-sm shadow-purple-500/50"></span>
              <span className="text-slate-200 font-medium">Role & Grupy ({stats.rolesCount})</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-emerald-500 shadow-sm shadow-emerald-500/50"></span>
              <span className="text-slate-200 font-medium">Zasoby ({stats.resourcesCount})</span>
            </div>
            <div className="hidden sm:flex items-center gap-1.5 text-indigo-400 font-mono">
              <ArrowRight className="w-3.5 h-3.5" />
              <span>{stats.tuplesCount} bezpośrednich relacji</span>
            </div>
            <div className="hidden md:flex items-center gap-1.5 text-amber-400 font-mono">
              <Sparkles className="w-3.5 h-3.5" />
              <span>{stats.transitiveCount} ścieżek dziedziczonych</span>
            </div>
          </div>

          {/* Quick Helper Tip */}
          <div className="flex items-center gap-2 text-slate-400">
            <Info className="w-3.5 h-3.5 text-indigo-400 shrink-0" />
            <span className="hidden lg:inline text-[11.5px]">
              Kliknij węzeł lub etykietę relacji, aby sprawdzić uprawnienie w OpenFGA Check API.
            </span>
          </div>
        </div>
      </div>
    </div>
  );
};

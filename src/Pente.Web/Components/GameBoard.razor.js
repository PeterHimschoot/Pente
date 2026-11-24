// Zoom, pan, pinch and stone placement for the Pente board SVG.
// The view is controlled by rewriting the SVG viewBox, so Blazor never re-renders during gestures.

const MAX_ZOOM = 6;
const DRAG_THRESHOLD = 6; // px before a press becomes a pan instead of a click

export function attach(svg, dotnet, size, margin, cell) {
    const full = margin * 2 + cell * (size - 1);
    const minW = full / MAX_ZOOM;
    const ghost = svg.querySelector('circle.ghost');
    let vb = { x: 0, y: 0, w: full };
    let anim = 0;

    const pointers = new Map(); // pointerId -> {x, y}
    let press = null;           // {x, y, moved, multi}
    let pinch = null;           // {dist, w}

    function apply() {
        svg.setAttribute('viewBox', `${vb.x} ${vb.y} ${vb.w} ${vb.w}`);
        svg.style.setProperty('--zoom', (full / vb.w).toFixed(2));
    }

    function clampView(v) {
        const w = Math.min(full, Math.max(minW, v.w));
        const x = Math.min(full - w, Math.max(0, v.x));
        const y = Math.min(full - w, Math.max(0, v.y));
        return { x, y, w };
    }

    function toSvg(clientX, clientY) {
        const rect = svg.getBoundingClientRect();
        return {
            x: vb.x + (clientX - rect.left) / rect.width * vb.w,
            y: vb.y + (clientY - rect.top) / rect.height * vb.w,
        };
    }

    // Zoom so that the board point under (px, py) stays fixed.
    function zoomAround(px, py, factor) {
        const w = Math.min(full, Math.max(minW, vb.w / factor));
        const k = w / vb.w;
        vb = clampView({ x: px - (px - vb.x) * k, y: py - (py - vb.y) * k, w });
        apply();
    }

    function animateTo(target) {
        cancelAnimationFrame(anim);
        const start = { ...vb };
        const t0 = performance.now();
        const step = (now) => {
            const t = Math.min(1, (now - t0) / 180);
            const e = 1 - Math.pow(1 - t, 3);
            vb = {
                x: start.x + (target.x - start.x) * e,
                y: start.y + (target.y - start.y) * e,
                w: start.w + (target.w - start.w) * e,
            };
            apply();
            if (t < 1) anim = requestAnimationFrame(step);
        };
        anim = requestAnimationFrame(step);
    }

    function nearestIntersection(clientX, clientY) {
        const p = toSvg(clientX, clientY);
        const col = Math.round((p.x - margin) / cell);
        const row = Math.round((p.y - margin) / cell);
        if (row < 0 || row >= size || col < 0 || col >= size) return null;
        // Ignore clicks too far from the intersection (e.g. in the label margin).
        const dx = p.x - (margin + col * cell), dy = p.y - (margin + row * cell);
        if (Math.hypot(dx, dy) > cell * 0.6) return null;
        return { row, col };
    }

    function canPlace(pt) {
        if (!pt || svg.dataset.interactive !== '1') return false;
        const board = svg.dataset.board || '';
        return board[pt.row * size + pt.col] === '0';
    }

    function showGhost(pt) {
        if (pt && canPlace(pt)) {
            ghost.setAttribute('cx', margin + pt.col * cell);
            ghost.setAttribute('cy', margin + pt.row * cell);
            ghost.setAttribute('visibility', 'visible');
        } else {
            ghost.setAttribute('visibility', 'hidden');
        }
    }

    // ---- event handlers ----

    function onWheel(e) {
        e.preventDefault();
        cancelAnimationFrame(anim);
        // Normalise line/page deltas to pixels.
        const dy = e.deltaMode === 1 ? e.deltaY * 16 : e.deltaMode === 2 ? e.deltaY * 400 : e.deltaY;
        const p = toSvg(e.clientX, e.clientY);
        zoomAround(p.x, p.y, Math.exp(-dy * 0.0015));
        showGhost(nearestIntersection(e.clientX, e.clientY));
    }

    function onPointerDown(e) {
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        cancelAnimationFrame(anim);
        svg.setPointerCapture(e.pointerId);
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pointers.size === 1) {
            press = { x: e.clientX, y: e.clientY, moved: false, multi: false };
        } else if (pointers.size === 2) {
            if (press) press.multi = true;
            const [a, b] = [...pointers.values()];
            pinch = { dist: Math.hypot(a.x - b.x, a.y - b.y), w: vb.w };
            showGhost(null);
        }
    }

    function onPointerMove(e) {
        if (!pointers.has(e.pointerId)) {
            if (e.pointerType === 'mouse') showGhost(nearestIntersection(e.clientX, e.clientY));
            return;
        }
        const prev = pointers.get(e.pointerId);
        const cur = { x: e.clientX, y: e.clientY };
        pointers.set(e.pointerId, cur);

        if (pointers.size >= 2 && pinch) {
            const [a, b] = [...pointers.values()];
            const dist = Math.hypot(a.x - b.x, a.y - b.y);
            if (dist > 0) {
                const mid = toSvg((a.x + b.x) / 2, (a.y + b.y) / 2);
                const targetW = pinch.w * pinch.dist / dist;
                zoomAround(mid.x, mid.y, vb.w / targetW);
                // Pan with the pinch midpoint as well.
                const rect = svg.getBoundingClientRect();
                const dx = ((cur.x - prev.x) / 2) / rect.width * vb.w;
                const dy = ((cur.y - prev.y) / 2) / rect.height * vb.w;
                vb = clampView({ x: vb.x - dx, y: vb.y - dy, w: vb.w });
                apply();
            }
            return;
        }

        if (!press) return;
        if (!press.moved && Math.hypot(cur.x - press.x, cur.y - press.y) > DRAG_THRESHOLD) {
            press.moved = true;
            svg.classList.add('panning');
            showGhost(null);
        }
        if (press.moved) {
            const rect = svg.getBoundingClientRect();
            const dx = (cur.x - prev.x) / rect.width * vb.w;
            const dy = (cur.y - prev.y) / rect.height * vb.w;
            vb = clampView({ x: vb.x - dx, y: vb.y - dy, w: vb.w });
            apply();
        } else if (e.pointerType === 'mouse') {
            showGhost(nearestIntersection(e.clientX, e.clientY));
        }
    }

    function onPointerUp(e) {
        if (!pointers.has(e.pointerId)) return;
        pointers.delete(e.pointerId);
        if (pointers.size < 2) pinch = null;

        if (pointers.size === 0) {
            svg.classList.remove('panning');
            const wasClick = press && !press.moved && !press.multi && e.type === 'pointerup';
            press = null;
            if (wasClick) {
                const pt = nearestIntersection(e.clientX, e.clientY);
                if (canPlace(pt)) {
                    showGhost(null);
                    dotnet.invokeMethodAsync('PlaceStone', pt.row, pt.col);
                }
            }
        } else if (pointers.size === 1 && press) {
            // Finished a pinch; continuing with one finger pans, never places.
            press.multi = true;
            press.moved = true;
        }
    }

    function onPointerLeave(e) {
        if (e.pointerType === 'mouse' && pointers.size === 0) showGhost(null);
    }

    svg.addEventListener('wheel', onWheel, { passive: false });
    svg.addEventListener('pointerdown', onPointerDown);
    svg.addEventListener('pointermove', onPointerMove);
    svg.addEventListener('pointerup', onPointerUp);
    svg.addEventListener('pointercancel', onPointerUp);
    svg.addEventListener('pointerleave', onPointerLeave);
    apply();

    return {
        zoomBy(factor) {
            const cx = vb.x + vb.w / 2, cy = vb.y + vb.w / 2;
            const w = Math.min(full, Math.max(minW, vb.w / factor));
            animateTo(clampView({ x: cx - w / 2, y: cy - w / 2, w }));
        },
        reset() {
            animateTo({ x: 0, y: 0, w: full });
        },
        detach() {
            cancelAnimationFrame(anim);
            svg.removeEventListener('wheel', onWheel);
            svg.removeEventListener('pointerdown', onPointerDown);
            svg.removeEventListener('pointermove', onPointerMove);
            svg.removeEventListener('pointerup', onPointerUp);
            svg.removeEventListener('pointercancel', onPointerUp);
            svg.removeEventListener('pointerleave', onPointerLeave);
        },
    };
}

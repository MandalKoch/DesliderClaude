// Pointer-based swipe-to-commit for the swipe page.
// Works under Blazor static SSR with enhanced navigation: on each enhancedload
// we re-query the DOM, reset any inline transforms left from the exit animation,
// and animate the new card in.

(() => {
    const SWIPE_THRESHOLD_PX = 120;
    const VELOCITY_THRESHOLD = 0.5;   // px/ms — Apple-ish flick
    const MIN_FLING_DISTANCE = 30;    // velocity-only commits still need some travel
    const MAX_ROTATION_DEG = 16;
    const DRAG_ROTATION_K = 0.08;
    const VERTICAL_DAMPING = 0.25;

    const EASE_OUT = 'cubic-bezier(0.23, 1, 0.32, 1)';

    function cancelExistingHandlers(card) {
        if (!card._swipeHandlers) return;
        const { down, move, up } = card._swipeHandlers;
        card.removeEventListener('pointerdown', down);
        card.removeEventListener('pointermove', move);
        card.removeEventListener('pointerup', up);
        card.removeEventListener('pointercancel', up);
        card._swipeHandlers = null;
    }

    function resetOverlays(yesEl, noEl) {
        if (yesEl) yesEl.style.opacity = '';
        if (noEl) noEl.style.opacity = '';
    }

    function playEnterAnimation(card) {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            card.style.transition = '';
            card.style.transform = '';
            card.style.opacity = '';
            return;
        }
        card.style.transition = 'none';
        card.style.transform = 'translate3d(0, 20px, 0) scale(0.96)';
        card.style.opacity = '0';
        // Force reflow so the transition runs from the above starting state.
        void card.offsetWidth;
        card.style.transition = `transform 320ms ${EASE_OUT}, opacity 260ms ease-out`;
        card.style.transform = '';
        card.style.opacity = '';
        setTimeout(() => {
            // Only clear if no drag is in progress (drag sets its own transitions).
            if (!card.classList.contains('is-dragging')) card.style.transition = '';
        }, 360);
    }

    function init() {
        const card = document.querySelector('[data-swipe-card]');
        if (!card) return;

        const form = document.querySelector('[data-swipe-form]');
        const btnYes = form && form.querySelector('[data-swipe-yes]');
        const btnNo = form && form.querySelector('[data-swipe-no]');
        const overlayYes = card.querySelector('[data-swipe-overlay-yes]');
        const overlayNo = card.querySelector('[data-swipe-overlay-no]');

        cancelExistingHandlers(card);
        resetOverlays(overlayYes, overlayNo);
        playEnterAnimation(card);

        if (!form || !btnYes || !btnNo) return;

        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        let dragging = false;
        let committed = false;
        let pointerId = null;
        let startX = 0;
        let startY = 0;
        let dx = 0;
        let dy = 0;
        let startTime = 0;

        const onPointerDown = (e) => {
            if (committed || dragging) return;
            // Ignore drags that originate on the buttons — let them click normally.
            if (e.target.closest('[data-swipe-yes], [data-swipe-no]')) return;
            if (e.button !== undefined && e.button !== 0) return;

            dragging = true;
            pointerId = e.pointerId;
            try { card.setPointerCapture(pointerId); } catch { /* ignore */ }
            startX = e.clientX;
            startY = e.clientY;
            dx = 0;
            dy = 0;
            startTime = performance.now();
            card.classList.add('is-dragging');
            card.style.transition = 'none';
        };

        const onPointerMove = (e) => {
            if (!dragging || e.pointerId !== pointerId) return;
            dx = e.clientX - startX;
            dy = (e.clientY - startY) * VERTICAL_DAMPING;
            const rotation = Math.max(-MAX_ROTATION_DEG, Math.min(MAX_ROTATION_DEG, dx * DRAG_ROTATION_K));
            card.style.transform = `translate3d(${dx}px, ${dy}px, 0) rotate(${rotation}deg)`;

            // Drag direction matches button position: left = Play (yes), right = Pass (no).
            if (overlayYes) overlayYes.style.opacity = Math.min(1, Math.max(0, -dx / SWIPE_THRESHOLD_PX));
            if (overlayNo) overlayNo.style.opacity = Math.min(1, Math.max(0, dx / SWIPE_THRESHOLD_PX));
        };

        const fling = (direction) => {
            if (committed) return;
            committed = true;
            card.classList.add('is-flinging');
            const button = direction === 'yes' ? btnYes : btnNo;
            // Yes = card flies left (toward the Play button), No = card flies right.
            const targetX = direction === 'yes' ? -(window.innerWidth + 220) : window.innerWidth + 220;
            const rot = direction === 'yes' ? -22 : 22;
            const overlayToPeak = direction === 'yes' ? overlayYes : overlayNo;
            if (overlayToPeak) overlayToPeak.style.opacity = '1';

            if (reduceMotion) {
                button.click();
                return;
            }
            card.style.transition = `transform 280ms ${EASE_OUT}, opacity 260ms ease-out`;
            card.style.transform = `translate3d(${targetX}px, 0, 0) rotate(${rot}deg)`;
            card.style.opacity = '0';
            // Submit a bit before the animation ends so server round-trip and
            // exit animation overlap. The new card will animate in via enhancedload.
            setTimeout(() => button.click(), 180);
        };

        const snapBack = () => {
            card.style.transition = `transform 280ms ${EASE_OUT}`;
            card.style.transform = '';
            if (overlayYes) overlayYes.style.transition = 'opacity 200ms ease-out';
            if (overlayNo) overlayNo.style.transition = 'opacity 200ms ease-out';
            resetOverlays(overlayYes, overlayNo);
            setTimeout(() => {
                card.style.transition = '';
                if (overlayYes) overlayYes.style.transition = '';
                if (overlayNo) overlayNo.style.transition = '';
            }, 300);
        };

        const onPointerUp = (e) => {
            if (!dragging || (pointerId !== null && e.pointerId !== pointerId)) return;
            dragging = false;
            try { card.releasePointerCapture(pointerId); } catch { /* ignore */ }
            pointerId = null;
            card.classList.remove('is-dragging');

            const elapsed = Math.max(1, performance.now() - startTime);
            const velocity = Math.abs(dx) / elapsed;
            const passDistance = Math.abs(dx) >= SWIPE_THRESHOLD_PX;
            const passVelocity = velocity > VELOCITY_THRESHOLD && Math.abs(dx) > MIN_FLING_DISTANCE;

            if (passDistance || passVelocity) {
                fling(dx < 0 ? 'yes' : 'no');
            } else {
                snapBack();
            }
        };

        card.addEventListener('pointerdown', onPointerDown);
        card.addEventListener('pointermove', onPointerMove);
        card.addEventListener('pointerup', onPointerUp);
        card.addEventListener('pointercancel', onPointerUp);
        card._swipeHandlers = { down: onPointerDown, move: onPointerMove, up: onPointerUp };
    }

    function hookBlazor() {
        if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
            window.Blazor.addEventListener('enhancedload', init);
        } else {
            setTimeout(hookBlazor, 50);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => { init(); hookBlazor(); });
    } else {
        init();
        hookBlazor();
    }
})();

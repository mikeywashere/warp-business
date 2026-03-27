/* ============================================================
   Warp Business Marketing Site — main.js
   Star-field canvas + nav scroll + mobile menu
   ============================================================ */

(function () {
  'use strict';

  /* ---- Starfield ---- */
  const canvas = document.getElementById('starfield');
  if (canvas) {
    const ctx = canvas.getContext('2d');
    let stars = [];
    let animId;

    function resize() {
      canvas.width = canvas.offsetWidth;
      canvas.height = canvas.offsetHeight;
    }

    function initStars(count) {
      stars = [];
      for (let i = 0; i < count; i++) {
        stars.push({
          x: Math.random() * canvas.width,
          y: Math.random() * canvas.height,
          r: Math.random() * 1.5 + 0.2,
          alpha: Math.random(),
          speed: Math.random() * 0.3 + 0.05,
          twinkleSpeed: Math.random() * 0.02 + 0.005,
          twinkleDir: Math.random() > 0.5 ? 1 : -1,
        });
      }
    }

    function drawStars() {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      for (const s of stars) {
        s.alpha += s.twinkleSpeed * s.twinkleDir;
        if (s.alpha >= 1) { s.alpha = 1; s.twinkleDir = -1; }
        if (s.alpha <= 0.1) { s.alpha = 0.1; s.twinkleDir = 1; }

        s.y += s.speed;
        if (s.y > canvas.height) {
          s.y = 0;
          s.x = Math.random() * canvas.width;
        }

        ctx.beginPath();
        ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(180, 220, 255, ${s.alpha})`;
        ctx.fill();
      }
      animId = requestAnimationFrame(drawStars);
    }

    function start() {
      cancelAnimationFrame(animId);
      resize();
      initStars(220);
      drawStars();
    }

    start();

    let resizeTimer;
    window.addEventListener('resize', function () {
      clearTimeout(resizeTimer);
      resizeTimer = setTimeout(start, 150);
    });
  }

  /* ---- Sticky nav scroll effect ---- */
  const nav = document.getElementById('nav');
  if (nav) {
    window.addEventListener('scroll', function () {
      nav.classList.toggle('nav--scrolled', window.scrollY > 40);
    }, { passive: true });
  }

  /* ---- Mobile menu toggle ---- */
  const toggle = document.getElementById('navToggle');
  const navLinks = document.getElementById('navLinks');
  if (toggle && navLinks) {
    toggle.addEventListener('click', function () {
      const open = navLinks.classList.toggle('nav__links--open');
      toggle.setAttribute('aria-expanded', String(open));
    });

    navLinks.querySelectorAll('a').forEach(function (link) {
      link.addEventListener('click', function () {
        navLinks.classList.remove('nav__links--open');
        toggle.setAttribute('aria-expanded', 'false');
      });
    });
  }

  /* ---- Scroll-reveal for sections ---- */
  const revealEls = document.querySelectorAll('.feature-card, .stat, .cta-section__inner');
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add('revealed');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.15 });
    revealEls.forEach(function (el) { observer.observe(el); });
  } else {
    revealEls.forEach(function (el) { el.classList.add('revealed'); });
  }

  /* ---- Smooth scroll for anchor links ---- */
  document.querySelectorAll('a[href^="#"]').forEach(function (anchor) {
    anchor.addEventListener('click', function (e) {
      const target = document.querySelector(this.getAttribute('href'));
      if (target) {
        e.preventDefault();
        target.scrollIntoView({ behavior: 'smooth' });
      }
    });
  });

})();

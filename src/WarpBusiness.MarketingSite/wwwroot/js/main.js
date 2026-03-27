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

  /* ---- Plugin Showcase Carousel ---- */
  // PLUGIN SHOWCASE — Add new plugins here when they are created in the solution.
  // Do NOT include WarpBusiness.Plugin.Sample — it is a developer scaffold template only.
  // Format: { icon, name, tagline, description, badge }
  var plugins = [
    {
      icon: '🎯',
      name: 'CRM',
      tagline: 'Customer Relationship Management',
      description: 'Track every contact, company, deal, and activity. Build relationships that scale.',
      badge: 'Available Now',
    },
    {
      icon: '👥',
      name: 'Employee Management',
      tagline: 'People Operations at Scale',
      description: 'Onboard, manage, and empower your workforce from a unified dashboard.',
      badge: 'Available Now',
    },
  ];

  var track = document.getElementById('pluginTrack');
  var dotsContainer = document.getElementById('pluginDots');

  if (track && dotsContainer && plugins.length > 0) {
    var current = 0;
    var rotateTimer = null;
    var INTERVAL = 4000;

    // Build cards
    var cards = plugins.map(function (p, i) {
      var card = document.createElement('div');
      card.className = 'plugin-card' + (i === 0 ? ' plugin-card--active' : '');
      card.setAttribute('role', 'tabpanel');
      card.setAttribute('aria-label', p.name);
      card.innerHTML =
        '<div class="plugin-card__icon">' + p.icon + '</div>' +
        '<span class="plugin-card__badge">' + p.badge + '</span>' +
        '<h3 class="plugin-card__name">' + p.name + '</h3>' +
        '<p class="plugin-card__tagline">' + p.tagline + '</p>' +
        '<p class="plugin-card__desc">' + p.description + '</p>';
      track.appendChild(card);
      return card;
    });

    // Set track min-height based on tallest card after paint
    function syncTrackHeight() {
      var max = 0;
      cards.forEach(function (c) {
        c.style.position = 'relative';
        var h = c.offsetHeight;
        c.style.position = '';
        if (h > max) max = h;
      });
      track.style.minHeight = max + 'px';
    }

    // Build dots
    var dots = plugins.map(function (p, i) {
      var btn = document.createElement('button');
      btn.className = 'plugin-dot' + (i === 0 ? ' plugin-dot--active' : '');
      btn.setAttribute('role', 'tab');
      btn.setAttribute('aria-label', 'Show ' + p.name + ' plugin');
      btn.setAttribute('aria-selected', i === 0 ? 'true' : 'false');
      btn.addEventListener('click', function () { goTo(i); });
      dotsContainer.appendChild(btn);
      return btn;
    });

    function goTo(index) {
      cards[current].classList.remove('plugin-card--active');
      dots[current].classList.remove('plugin-dot--active');
      dots[current].setAttribute('aria-selected', 'false');
      current = (index + plugins.length) % plugins.length;
      cards[current].classList.add('plugin-card--active');
      dots[current].classList.add('plugin-dot--active');
      dots[current].setAttribute('aria-selected', 'true');
    }

    function startRotation() {
      stopRotation();
      rotateTimer = setInterval(function () {
        goTo(current + 1);
      }, INTERVAL);
    }

    function stopRotation() {
      clearInterval(rotateTimer);
    }

    var carousel = document.getElementById('pluginCarousel');
    if (carousel) {
      carousel.addEventListener('mouseenter', stopRotation);
      carousel.addEventListener('mouseleave', startRotation);
      carousel.addEventListener('focusin', stopRotation);
      carousel.addEventListener('focusout', startRotation);
    }

    window.addEventListener('load', syncTrackHeight);
    startRotation();
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

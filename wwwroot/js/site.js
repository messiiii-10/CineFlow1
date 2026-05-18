(() => {
  const initSearchScroll = () => {
    const form = document.querySelector("[data-search-form]");
    const storageKey = "cineflow:search-scroll-target";

    if (form) {
      form.addEventListener("submit", () => {
        const targetId = form.getAttribute("data-scroll-target");
        if (targetId) {
          sessionStorage.setItem(storageKey, targetId);
        }
      });
    }

    const targetId = sessionStorage.getItem(storageKey);
    if (!targetId) {
      return;
    }

    const hasSearchParams = new URLSearchParams(window.location.search).toString().length > 0;
    if (!hasSearchParams) {
      sessionStorage.removeItem(storageKey);
      return;
    }

    window.setTimeout(() => {
      const target = document.getElementById(targetId);
      if (target) {
        target.scrollIntoView({ behavior: "smooth", block: "start" });
      }

      sessionStorage.removeItem(storageKey);
    }, 180);
  };

  const initImageFallbacks = () => {
    const showFallback = (image) => {
      const frame = image.closest("[data-media-frame]");
      const fallback = frame?.querySelector("[data-media-fallback]");

      if (!frame || !fallback || frame.classList.contains("is-broken")) {
        return;
      }

      frame.classList.add("is-broken");
      fallback.hidden = false;
      image.setAttribute("aria-hidden", "true");
    };

    document.querySelectorAll("[data-fallback-image]").forEach((image) => {
      image.addEventListener("error", () => showFallback(image), { once: true });

      if (image.complete && image.naturalWidth === 0) {
        showFallback(image);
      }
    });
  };

  initSearchScroll();
  initImageFallbacks();
})();

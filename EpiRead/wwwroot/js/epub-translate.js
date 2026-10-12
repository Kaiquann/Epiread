// @author: Chong Jun Xiang
// @description: Translation logic for EPUB reader
(function () {
  // Standalone translation logic for EPUB reader.
  // This script expects the EPUB rendition to be exposed as window.epub_rendition
  // and the translate toolbar element with id `translateToolbar` to exist in the DOM.

  function waitForRenditionAndInit(maxAttempts = 50, interval = 100) {
    let attempts = 0;
    const timer = setInterval(() => {
      if (window.epub_rendition) {
        clearInterval(timer);
        try {
          initTranslation(window.epub_rendition);
        } catch (e) {
          console.error("initTranslation failed", e);
        }
        return;
      }
      attempts++;
      if (attempts >= maxAttempts) {
        clearInterval(timer);
        console.warn(
          "epub_rendition not found; translation features will not initialize."
        );
      }
    }, interval);
  }

  function initTranslation(rendition) {
    const toolbar = document.getElementById("translateToolbar");
    // move toolbar to document.body so it's not clipped by any transformed parent or container
    try {
      if (toolbar && toolbar.parentNode !== document.body) {
        document.body.appendChild(toolbar);
        toolbar.style.position = "fixed";
        toolbar.style.pointerEvents = "auto";
      }
    } catch (e) {
      /* ignore if append fails */
    }
    const fromLang = document.getElementById("fromLang");
    const toLang = document.getElementById("toLang");
    const translateBtn = document.getElementById("translateBtn");
    const closeBtn = document.getElementById("closeTranslateBtn");

    // Allow larger text selections to be translated (aligned with Google Translate limit of 5000 chars)
    const MAX_SELECTION_CHARS = 5000;

    // In-memory storage for translation data to avoid HTML encoding issues with data attributes
    // This ensures accurate character counting without HTML entity encoding
    const translationState = {
      selectedText: "",
      translateId: null,
    };

    // Helper: Escape HTML
    function escapeHtml(str) {
      return String(str)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
    }

    // Helper: Get language name from code
    function getLanguageName(code) {
      // Keys are lower-case to support case-insensitive lookups.
      const langMap = {
        auto: "Auto",
        af: "Afrikaans",
        sq: "Albanian",
        ar: "Arabic",
        hy: "Armenian",
        eu: "Basque",
        be: "Belarusian",
        bn: "Bengali",
        bs: "Bosnian",
        bg: "Bulgarian",
        ca: "Catalan",
        zh: "Chinese",
        hr: "Croatian",
        cs: "Czech",
        da: "Danish",
        nl: "Dutch",
        en: "English",
        et: "Estonian",
        fi: "Finnish",
        fr: "French",
        gl: "Galician",
        ka: "Georgian",
        de: "German",
        el: "Greek",
        gu: "Gujarati",
        he: "Hebrew",
        hi: "Hindi",
        hu: "Hungarian",
        is: "Icelandic",
        id: "Indonesian",
        ga: "Irish",
        it: "Italian",
        ja: "Japanese",
        jv: "Javanese",
        kn: "Kannada",
        kk: "Kazakh",
        ko: "Korean",
        ku: "Kurdish",
        ky: "Kyrgyz",
        lv: "Latvian",
        lt: "Lithuanian",
        lb: "Luxembourgish",
        mk: "Macedonian",
        ms: "Malay",
        ml: "Malayalam",
        mt: "Maltese",
        mi: "Maori",
        mr: "Marathi",
        mn: "Mongolian",
        ne: "Nepali",
        no: "Norwegian",
        oc: "Occitan",
        or: "Oriya",
        ps: "Pashto",
        fa: "Persian",
        pl: "Polish",
        pt: "Portuguese",
        pa: "Punjabi",
        ro: "Romanian",
        ru: "Russian",
        sa: "Sanskrit",
        sr: "Serbian",
        sk: "Slovak",
        sl: "Slovenian",
        es: "Spanish",
        sw: "Swahili",
        sv: "Swedish",
        ta: "Tamil",
        te: "Telugu",
        th: "Thai",
        tr: "Turkish",
        uk: "Ukrainian",
        ur: "Urdu",
        uz: "Uzbek",
        vi: "Vietnamese",
        cy: "Welsh",
        xh: "Xhosa",
        yo: "Yoruba",
        zu: "Zulu",
      };
      // allow locale variants such as 'en-US' by using the base language
      const base = (code || "").split(/[\-_]/)[0].toLowerCase();
      return langMap[base] || code.toUpperCase();
    }

    // Helper: Copy to clipboard
    function copyToClipboard(text, button) {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard
          .writeText(text)
          .then(() => {
            showCopyFeedback(button);
          })
          .catch(() => {
            fallbackCopy(text, button);
          });
      } else {
        fallbackCopy(text, button);
      }
    }

    function fallbackCopy(text, button) {
      const textarea = document.createElement("textarea");
      textarea.value = text;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.select();
      try {
        document.execCommand("copy");
        showCopyFeedback(button);
      } catch (err) {
        console.error("Copy failed", err);
      }
      document.body.removeChild(textarea);
    }

    function showCopyFeedback(button) {
      button.classList.add("copied");
      const originalText = button.innerHTML;
      button.innerHTML =
        '<i class="bi bi-check-circle-fill"></i><span>Copied!</span>';
      setTimeout(() => {
        button.classList.remove("copied");
        button.innerHTML = originalText;
      }, 2000);
    }

    // Helper: Create loading bubble
    function createLoadingBubble() {
      const existing = document.querySelector(".translation-bubble");
      if (existing && existing.parentNode) {
        existing.classList.add("closing");
        setTimeout(() => {
          if (existing.parentNode) existing.parentNode.removeChild(existing);
        }, 200);
      }

      const bubble = document.createElement("div");
      bubble.className = "translation-bubble";
      bubble.innerHTML = `
        <div class="bubble-loading">
          <div class="bubble-spinner"></div>
          <div class="bubble-loading-text">Processing Translation...</div>
        </div>
      `;

      document.body.appendChild(bubble);
      bubble.style.position = "fixed";
      bubble.style.pointerEvents = "auto";

      // Position the loading bubble near the toolbar
      try {
        if (toolbar) {
          const toolbarRect = toolbar.getBoundingClientRect();
          const bw = bubble.offsetWidth || 320;
          const bh = bubble.offsetHeight || 150;
          let left = Math.round(
            toolbarRect.left + toolbarRect.width / 2 - bw / 2
          );
          let top = Math.round(toolbarRect.bottom + 8);
          const vw = window.innerWidth || document.documentElement.clientWidth;
          const vh =
            window.innerHeight || document.documentElement.clientHeight;
          left = Math.max(16, Math.min(left, vw - bw - 16));
          top = Math.max(16, Math.min(top, vh - bh - 16));
          bubble.style.left = left + "px";
          bubble.style.top = top + "px";
          bubble.style.zIndex = "2147483647";
        }
      } catch (e) {
        console.error("Error positioning loading bubble:", e);
      }

      return bubble;
    }

    // Helper: Create enhanced bubble
    function createEnhancedBubble(
      originalText,
      translation,
      fromLangCode,
      toLangCode
    ) {
      // Remove any existing bubbles (including loading bubble)
      const existing = document.querySelector(".translation-bubble");
      if (existing && existing.parentNode) {
        existing.classList.add("closing");
        setTimeout(() => {
          if (existing.parentNode) existing.parentNode.removeChild(existing);
        }, 200);
      }

      const bubble = document.createElement("div");
      bubble.className = "translation-bubble";

      const fromLang = getLanguageName(fromLangCode);
      const toLang = getLanguageName(toLangCode);

      bubble.innerHTML = `
        <div class="bubble-header">
          <div class="bubble-lang-info">
            <span class="bubble-lang-badge">${escapeHtml(fromLang)}</span>
            <i class="bi bi-arrow-right bubble-arrow"></i>
            <span class="bubble-lang-badge">${escapeHtml(toLang)}</span>
          </div>
          <button class="bubble-close" title="Close" aria-label="Close translation bubble">×</button>
        </div>
        <div class="bubble-content">
          <div class="bubble-section">
            <div class="bubble-label">
              <i class="bi bi-file-text"></i>
              <span>Original Text</span>
            </div>
            <div class="bubble-original">${escapeHtml(originalText)}</div>
          </div>
          <div class="bubble-section">
            <div class="bubble-label">
              <i class="bi bi-translate"></i>
              <span>Translation</span>
            </div>
            <div class="bubble-translation">${escapeHtml(translation)}</div>
          </div>
        </div>
        <div class="bubble-footer">
          <button class="bubble-action-btn" data-action="copy-original" title="Copy original text">
            <i class="bi bi-clipboard"></i>
            <span>Copy Original</span>
          </button>
          <button class="bubble-action-btn" data-action="copy-translation" title="Copy translation">
            <i class="bi bi-clipboard-check"></i>
            <span>Copy Translation</span>
          </button>
        </div>
      `;

      return bubble;
    }

    function hideToolbar() {
      if (!toolbar) return;
      toolbar.classList.remove("visible");
      setTimeout(() => {
        try {
          toolbar.style.display = "none";
        } catch (e) {}
      }, 180);
      toolbar.setAttribute("aria-hidden", "true");
    }

    function showToolbarAt(x, y) {
      if (!toolbar) return;
      toolbar.style.left = x + "px";
      toolbar.style.top = y + "px";
      toolbar.style.display = "block";
      // small entrance transform
      toolbar.classList.add("visible");
      toolbar.setAttribute("aria-hidden", "false");

      // Reset language selection to 'auto' UNLESS user explicitly set a language
      // This ensures each new selection starts fresh with auto-detection
      if (fromLang && !fromLang.dataset.userSelected) {
        fromLang.value = "auto";
      }

      // Clear detected language display and data attribute for new selection
      try {
        const detected = toolbar.querySelector(".detected-lang");
        if (detected) detected.textContent = "";
        if (fromLang) delete fromLang.dataset.detected;
      } catch (e) {
        /* ignore */
      }
    }

    if (!toolbar) return;

    closeBtn && closeBtn.addEventListener("click", hideToolbar);
    if (closeBtn) closeBtn.addEventListener("click", hideToolbar);

    // accessibility
    toolbar.setAttribute("role", "dialog");
    toolbar.setAttribute("aria-label", "Translation toolbar");

    // Track whether the user explicitly selected a source language.
    // If the user hasn't made a selection, we consider the source language as 'auto'.
    try {
      if (fromLang) {
        fromLang.addEventListener("change", function () {
          if (this.value && this.value.toLowerCase() === "auto") {
            // clear flag when the user selected Auto again
            delete this.dataset.userSelected;
          } else {
            this.dataset.userSelected = "true";
          }
        });
      }
    } catch (e) {
      /* ignore */
    }

    // create small UI elements: detected language and spinner
    try {
      const inner = toolbar.querySelector(".d-flex") || toolbar;
      let detected = inner.querySelector(".detected-lang");
      if (!detected) {
        detected = document.createElement("span");
        detected.className = "detected-lang";
        detected.textContent = "";
        inner.appendChild(detected);
      }
      let spinner = inner.querySelector(".loading-spinner");
      if (!spinner) {
        spinner = document.createElement("span");
        spinner.className = "loading-spinner";
        spinner.style.display = "none";
        inner.appendChild(spinner);
      }

      function setBusy(isBusy) {
        toolbar.setAttribute("aria-busy", isBusy ? "true" : "false");
        if (isBusy) {
          spinner.style.display = "inline-block";
          if (translateBtn) translateBtn.setAttribute("disabled", "disabled");
          if (fromLang) fromLang.setAttribute("disabled", "disabled");
          if (toLang) toLang.setAttribute("disabled", "disabled");
        } else {
          spinner.style.display = "none";
          if (translateBtn) translateBtn.removeAttribute("disabled");
          if (fromLang) fromLang.removeAttribute("disabled");
          if (toLang) toLang.removeAttribute("disabled");
        }
      }

      function updateDetectedLanguageUI(respData) {
        try {
          const detectedCode =
            Array.isArray(respData) && respData[2] ? respData[2] : null;
          if (detectedCode) {
            detected.textContent = "Detected: " + detectedCode.toUpperCase();
            // show detected language but do NOT change the selected source language
            // (preserve 'Auto' so user can keep automatic detection across multiple
            // translations). If you want to allow users to explicitly set the detected
            // language, add a separate UI action—don't change the select value silently.
            // Keep for compatibility: set a data attribute so other code can read it
            // without changing the user's current selection.
            if (fromLang)
              fromLang.dataset.detected = detectedCode.toUpperCase();
          } else {
            detected.textContent = "";
          }
        } catch (e) {
          /* ignore */
        }
      }
      // expose helpers locally
      toolbar._setBusy = setBusy;
      toolbar._updateDetected = updateDetectedLanguageUI;
    } catch (e) {
      /* ignore UI helper creation errors */
    }

    // keyboard shortcut: Ctrl/Cmd+T to show toolbar near current selection
    document.addEventListener("keydown", function (ev) {
      if (
        (ev.ctrlKey || ev.metaKey) &&
        ev.key &&
        ev.key.toLowerCase() === "t"
      ) {
        ev.preventDefault();
        tryShowToolbarForSelection();
      }
    });

    function tryShowToolbarForSelection() {
      try {
        const iframe = document.querySelector("#epub-viewer iframe");
        if (!iframe) return;
        const idoc = iframe.contentDocument || iframe.contentWindow.document;
        if (!idoc) return;
        const sel = idoc.getSelection();
        if (!sel || sel.isCollapsed) return;
        const range = sel.getRangeAt(0);
        const rect = range.getBoundingClientRect();
        const iframeRect = iframe.getBoundingClientRect();
        const x = iframeRect.left + rect.left;
        const y = iframeRect.top + rect.top - (toolbar.offsetHeight || 36) - 8;
        showToolbarAt(x, y);
        const selectedText = sel.toString().trim();
        if (selectedText && selectedText.length <= MAX_SELECTION_CHARS) {
          translationState.selectedText = selectedText;
          translationState.translateId = null;
        } else {
          // ignore too-large selections invoked via keyboard shortcut
          return;
        }
      } catch (e) {
        /* ignore */
      }
    }

    // hide when clicking outside toolbar (but allow clicks in epub iframe)
    document.addEventListener("click", function (e) {
      if (!toolbar.contains(e.target)) hideToolbar();
    });

    // attach listener when a page is rendered
    if (rendition && typeof rendition.on === "function") {
      rendition.on("rendered", function (section) {
        // when a new page is rendered, remove any existing floating bubbles to avoid stale overlays
        try {
          if (window._translationBubbles) {
            Object.keys(window._translationBubbles).forEach((k) => {
              try {
                const ent = window._translationBubbles[k];
                if (ent && ent.bubble && ent.bubble.parentNode)
                  ent.bubble.parentNode.removeChild(ent.bubble);
              } catch (e) {}
              try {
                delete window._translationBubbles[k];
              } catch (e) {}
            });
          }
        } catch (e) {
          /* ignore */
        }
        try {
          const iframe = document.querySelector("#epub-viewer iframe");
          if (!iframe) return;
          const idoc = iframe.contentDocument || iframe.contentWindow.document;
          if (!idoc) return;

          // avoid attaching multiple listeners
          if (idoc._translationAttached) return;
          idoc._translationAttached = true;

          idoc.addEventListener(
            "mouseup",
            function (ev) {
              try {
                const sel = idoc.getSelection();
                if (!sel || sel.isCollapsed) return;
                const rawText = sel.toString().trim();
                if (!rawText) return;

                const range = sel.getRangeAt(0);

                // determine if selection is inside an existing translated-inline span
                let container = range.commonAncestorContainer;
                if (container.nodeType === 3) container = container.parentNode;
                // use closest if available
                let translatedSpan = null;
                if (container && typeof container.closest === "function") {
                  translatedSpan = container.closest(
                    ".translated-inline, .translation-anchor"
                  );
                } else {
                  // fallback traverse upwards
                  let p = container;
                  while (p && p !== idoc.body) {
                    if (
                      p.classList &&
                      p.classList.contains &&
                      p.classList.contains("translated-inline")
                    ) {
                      translatedSpan = p;
                      break;
                    }
                    p = p.parentNode;
                  }
                }

                let selectedForTranslate = rawText;
                if (translatedSpan) {
                  // prefer stored original text when selecting translated content
                  const orig =
                    translatedSpan.getAttribute("data-original") ||
                    (translatedSpan.querySelector &&
                    translatedSpan.querySelector(".orig")
                      ? translatedSpan.querySelector(".orig").textContent
                      : rawText);
                  selectedForTranslate = (orig || rawText).toString().trim();
                }

                if (!selectedForTranslate) return;

                // enforce size only for non-translated selections
                if (
                  !translatedSpan &&
                  selectedForTranslate.length > MAX_SELECTION_CHARS
                ) {
                  if (window.Swal) {
                    window.Swal.fire(
                      "Selection too large",
                      "Please select shorter text to translate (max " +
                        MAX_SELECTION_CHARS +
                        " characters).",
                      "warning"
                    );
                  } else {
                    alert(
                      "Please select shorter text to translate (max " +
                        MAX_SELECTION_CHARS +
                        " characters)."
                    );
                  }
                  return;
                }

                const rect = range.getBoundingClientRect();
                const iframeRect = iframe.getBoundingClientRect();
                const x = iframeRect.left + rect.left;
                const y =
                  iframeRect.top + rect.top - (toolbar.offsetHeight || 36) - 8;
                showToolbarAt(x, y);

                // store original selection text to translate; if selection inside translated span use its original
                translationState.selectedText = selectedForTranslate;
                // if selecting a translated span, also store its id so Undo is available
                if (translatedSpan) {
                  const tid = translatedSpan.getAttribute("data-translate-id");
                  if (tid) translationState.translateId = tid;
                } else {
                  // clear any previous targeted translate id when selecting fresh text
                  translationState.translateId = null;
                }

                // Reset language selection to 'auto' UNLESS user explicitly set a language
                // This is needed here because selection event may not trigger showToolbarAt in all cases
                if (fromLang && !fromLang.dataset.userSelected) {
                  fromLang.value = "auto";
                }

                // Clear detected language display and data for new selection
                try {
                  const detected = toolbar.querySelector(".detected-lang");
                  if (detected) detected.textContent = "";
                  if (fromLang) delete fromLang.dataset.detected;
                } catch (e) {
                  /* ignore */
                }
              } catch (ie) {
                console.warn("selection handling error", ie);
              }
            },
            false
          );
          // allow clicking on translated spans to show toolbar for undo
          idoc.addEventListener(
            "click",
            function (ev) {
              try {
                // check both anchors and inline spans
                const el = ev.target.closest
                  ? ev.target.closest(".translated-inline") ||
                    ev.target.closest(".translation-anchor")
                  : null;
                if (el) {
                  const rect = el.getBoundingClientRect();
                  const iframeRect = iframe.getBoundingClientRect();
                  const x = iframeRect.left + rect.left;
                  const y =
                    iframeRect.top +
                    rect.top -
                    (toolbar.offsetHeight || 36) -
                    8;
                  showToolbarAt(x, y);
                  const tid = el.getAttribute("data-translate-id");
                  if (tid) translationState.translateId = tid;
                  // clear any selected text payload
                  translationState.selectedText = "";

                  // ensure floating bubble visible (if we created one earlier)
                  if (
                    window._translationBubbles &&
                    window._translationBubbles[tid]
                  ) {
                    const entry = window._translationBubbles[tid];
                    if (entry && entry.bubble) {
                      entry.bubble.style.display = "block";
                    }
                  } else {
                    // recreate bubble from attributes if present on element
                    try {
                      const orig = el.getAttribute("data-original") || "";
                      const trans = el.getAttribute("data-translation") || "";
                      if (orig || trans) {
                        const uid =
                          el.getAttribute("data-translate-id") ||
                          "tr-" + Date.now();

                        // Create enhanced bubble
                        const bubble = createEnhancedBubble(
                          orig,
                          trans,
                          fromLang.value,
                          toLang.value
                        );
                        bubble.setAttribute("data-translate-id", uid);
                        bubble.style.position = "fixed";
                        document.body.appendChild(bubble);

                        const viewportWidth =
                          window.innerWidth ||
                          document.documentElement.clientWidth;
                        let left = Math.round(
                          iframeRect.left +
                            rect.left +
                            rect.width / 2 -
                            bubble.offsetWidth / 2
                        );
                        left = Math.max(
                          8,
                          Math.min(left, viewportWidth - bubble.offsetWidth - 8)
                        );
                        const bh = bubble.offsetHeight;
                        let top = Math.round(
                          iframeRect.top + rect.top - bh - 18
                        );
                        top = Math.max(8, top);
                        bubble.style.left = left + "px";
                        bubble.style.top = top + "px";
                        bubble.style.zIndex = "2147483647";
                        bubble.style.pointerEvents = "auto";

                        // Close button handler
                        bubble
                          .querySelector(".bubble-close")
                          .addEventListener("click", function () {
                            try {
                              if (bubble.parentNode)
                                bubble.parentNode.removeChild(bubble);
                            } catch (e) {}
                            if (window._translationBubbles)
                              delete window._translationBubbles[uid];
                          });

                        // Copy button handlers
                        bubble
                          .querySelectorAll(".bubble-action-btn")
                          .forEach((btn) => {
                            btn.addEventListener("click", function () {
                              const action = this.getAttribute("data-action");
                              if (action === "copy-original") {
                                copyToClipboard(orig, this);
                              } else if (action === "copy-translation") {
                                copyToClipboard(trans, this);
                              }
                            });
                          });

                        window._translationBubbles =
                          window._translationBubbles || {};
                        window._translationBubbles[uid] = {
                          bubble: bubble,
                          anchorExists:
                            !!el.classList.contains("translation-anchor"),
                        };
                      }
                    } catch (re) {
                      console.warn("recreate bubble failed", re);
                    }
                  }
                }
              } catch (e) {
                console.warn("click on translated element failed", e);
              }
            },
            false
          );
        } catch (e) {
          console.warn(
            "Could not attach selection listener to rendered content",
            e
          );
        }
      });
    }

    translateBtn &&
      translateBtn.addEventListener("click", function (e) {
        e.preventDefault();
        const selectedText = translationState.selectedText;
        if (!selectedText) return;
        const sl = fromLang && fromLang.value ? fromLang.value : "auto";
        const tl = toLang && toLang.value ? toLang.value : "EN";

        // Ensure selection length does not exceed the configured limit
        if (selectedText && selectedText.length > MAX_SELECTION_CHARS) {
          if (window.Swal) {
            window.Swal.fire(
              "Text too long",
              `Selection exceeds the maximum allowed translation length (${MAX_SELECTION_CHARS} characters).`,
              "warning"
            );
          } else {
            alert(
              `Selection exceeds the maximum allowed translation length (${MAX_SELECTION_CHARS} characters).`
            );
          }
          hideToolbar();
          return;
        }

        // Show loading bubble
        const loadingBubble = createLoadingBubble();

        // Use server-side proxy to avoid CORS and quota issues
        const payload = { text: selectedText, sl: sl, tl: tl };

        fetch("/Translate/TranslateText", {
          method: "POST",
          headers: { "Content-Type": "application/x-www-form-urlencoded" },
          body: new URLSearchParams(payload),
        })
          .then((r) => r.json())
          .then((data) => {
            if (data && data.error) {
              // Server-side validation (e.g., text too long)
              if (window.Swal) {
                window.Swal.fire("Translation error", data.error, "warning");
              } else {
                alert(data.error);
              }

              if (loadingBubble && loadingBubble.parentNode) {
                loadingBubble.classList.add("closing");
                setTimeout(() => {
                  if (loadingBubble.parentNode)
                    loadingBubble.parentNode.removeChild(loadingBubble);
                }, 200);
              }
              return;
            }
            // Remove loading bubble
            if (loadingBubble && loadingBubble.parentNode) {
              loadingBubble.classList.add("closing");
              setTimeout(() => {
                if (loadingBubble.parentNode)
                  loadingBubble.parentNode.removeChild(loadingBubble);
              }, 200);
            }
            let translation = "";
            try {
              if (Array.isArray(data) && Array.isArray(data[0])) {
                translation = data[0].map((part) => part[0]).join("");
              }
            } catch (err) {
              console.error(err);
            }

            if (!translation) {
              if (window.Swal)
                window.Swal.fire(
                  "Translation error",
                  "Could not parse translation response.",
                  "error"
                );
              return;
            }

            // insert a zero-width anchor at the selection and create a floating bubble in parent
            if (!translation) {
              if (window.Swal)
                window.Swal.fire(
                  "Translation error",
                  "Could not parse translation response.",
                  "error"
                );
              return;
            }
            try {
              const iframe = document.querySelector("#epub-viewer iframe");
              if (!iframe) {
                if (window.Swal)
                  window.Swal.fire(
                    "Insert failed",
                    "EPUB frame not found.",
                    "error"
                  );
                return;
              }
              // show loading state using the toolbar helper (preferred) so we do
              // not modify the Translate button text directly and avoid it getting
              // stuck if an error occurs. Fallback to disabling button if helper
              // is not present.
              try {
                if (toolbar && typeof toolbar._setBusy === "function") {
                  toolbar._setBusy(true);
                } else if (translateBtn) {
                  translateBtn.setAttribute("disabled", "disabled");
                  translateBtn.dataset.origText = translateBtn.textContent;
                  translateBtn.textContent = "Translating...";
                }
              } catch (busyErr) {
                // ensure we don't break insertion flow if busy handling errors
                console.warn("Failed to set busy state", busyErr);
              }
              const idoc =
                iframe.contentDocument || iframe.contentWindow.document;
              const sel = idoc.getSelection ? idoc.getSelection() : null;
              // ensure a global map for bubbles
              window._translationBubbles = window._translationBubbles || {};

              if (sel && !sel.isCollapsed) {
                const range = sel.getRangeAt(0);
                const uid =
                  "tr-" + Date.now() + "-" + Math.floor(Math.random() * 10000);

                // create zero-width anchor so we don't replace visible content
                const anchor = idoc.createElement("span");
                anchor.className = "translation-anchor";
                anchor.setAttribute("data-translate-id", uid);
                anchor.setAttribute("data-original", selectedText);
                anchor.setAttribute("data-translation", translation);
                // make it unobtrusive
                anchor.style.display = "inline-block";
                anchor.style.width = "0px";
                anchor.style.height = "0px";
                anchor.style.lineHeight = "0";
                anchor.style.verticalAlign = "baseline";

                // insert anchor at the end of selection
                try {
                  range.collapse(false);
                } catch (e) {
                  /* ignore */
                }
                range.insertNode(anchor);
                sel.removeAllRanges();

                // create floating bubble in parent document (use fixed positioning so it stays above layout)
                const bubble = createEnhancedBubble(
                  selectedText,
                  translation,
                  fromLang.value,
                  toLang.value
                );
                bubble.setAttribute("data-translate-id", uid);
                bubble.style.position = "fixed";
                document.body.appendChild(bubble);

                // update detected language UI if the toolbar helper exists
                try {
                  if (toolbar && typeof toolbar._updateDetected === "function")
                    toolbar._updateDetected(data);
                } catch (e) {}

                // position bubble centered above anchor with extra upward offset and viewport clamping
                const anchorRect = anchor.getBoundingClientRect();
                const iframeRect = iframe.getBoundingClientRect();
                const viewportWidth =
                  window.innerWidth || document.documentElement.clientWidth;
                const viewportHeight =
                  window.innerHeight || document.documentElement.clientHeight;

                // initial left/top relative to viewport
                let left = Math.round(
                  iframeRect.left +
                    anchorRect.left +
                    anchorRect.width / 2 -
                    bubble.offsetWidth / 2
                );
                let top = Math.round(
                  iframeRect.top + anchorRect.top - bubble.offsetHeight - 18
                ); // make it higher by default

                // clamp left within viewport with 8px padding
                left = Math.max(
                  8,
                  Math.min(left, viewportWidth - bubble.offsetWidth - 8)
                );
                // clamp top so bubble is visible (avoid being under the page header); prefer higher if close to top
                top = Math.max(8, top);

                bubble.style.left = left + "px";
                bubble.style.top = top + "px";

                // wire close button to remove anchor+bubble
                bubble
                  .querySelector(".bubble-close")
                  .addEventListener("click", function () {
                    try {
                      const a = idoc.querySelector(
                        `[data-translate-id="${uid}"]`
                      );
                      if (a && a.parentNode) a.parentNode.removeChild(a);
                    } catch (er) {
                      console.warn(er);
                    }
                    if (bubble && bubble.parentNode)
                      bubble.parentNode.removeChild(bubble);
                    delete window._translationBubbles[uid];
                  });

                // Copy button handlers
                bubble.querySelectorAll(".bubble-action-btn").forEach((btn) => {
                  btn.addEventListener("click", function () {
                    const action = this.getAttribute("data-action");
                    if (action === "copy-original") {
                      copyToClipboard(selectedText, this);
                    } else if (action === "copy-translation") {
                      copyToClipboard(translation, this);
                    }
                  });
                });

                // store mapping
                window._translationBubbles[uid] = {
                  bubble: bubble,
                  anchorExists: true,
                };
                translationState.translateId = uid;

                // restore busy state on toolbar or translate button fallback
                try {
                  if (toolbar && typeof toolbar._setBusy === "function") {
                    toolbar._setBusy(false);
                  } else if (translateBtn) {
                    translateBtn.removeAttribute("disabled");
                    if (translateBtn.dataset.origText)
                      translateBtn.textContent = translateBtn.dataset.origText;
                  }
                } catch (busyErr) {
                  console.warn("Failed to restore busy state", busyErr);
                }

                // attach listener in iframe so clicking the anchor shows toolbar and bubble
                try {
                  anchor.addEventListener("click", function (ev) {
                    ev.stopPropagation();
                    const rect = anchor.getBoundingClientRect();
                    const iframeRect2 = iframe.getBoundingClientRect();
                    const x = iframeRect2.left + rect.left;
                    const y =
                      iframeRect2.top +
                      rect.top -
                      (toolbar.offsetHeight || 36) -
                      8;
                    showToolbarAt(x, y);
                    translationState.translateId = uid;
                    translationState.selectedText = "";
                  });
                } catch (ee) {
                  /* ignore */
                }
              } else {
                // fallback: try best-effort replace as before but keep translated-inline
                const bodyHtml = idoc.body.innerHTML;
                const idx = bodyHtml.indexOf(selectedText);
                if (idx >= 0) {
                  const fallbackId = "tr-fallback-" + Date.now();
                  idoc.body.innerHTML = bodyHtml.replace(
                    selectedText,
                    `<span class="translated-inline" data-translate-id="${fallbackId}" data-original="${selectedText}" data-translation="${translation}"><span class="orig">${selectedText}</span><span class="trans">${translation}</span></span>`
                  );
                  translationState.translateId = fallbackId;
                } else {
                  if (window.Swal)
                    window.Swal.fire(
                      "Insert failed",
                      "Could not find selection to replace.",
                      "error"
                    );
                }
              }
            } catch (ex) {
              console.error("Injection error", ex);
              if (window.Swal)
                window.Swal.fire(
                  "Error",
                  "Failed to insert translation into EPUB content. See console for details.",
                  "error"
                );
            } finally {
              // Ensure busy state is always cleared so button is not stuck on Translating...
              try {
                if (toolbar && typeof toolbar._setBusy === "function") {
                  toolbar._setBusy(false);
                } else if (translateBtn) {
                  translateBtn.removeAttribute("disabled");
                  if (translateBtn.dataset.origText)
                    translateBtn.textContent = translateBtn.dataset.origText;
                }
              } catch (busyErr) {
                console.warn("Failed to restore busy state in finally", busyErr);
              }
            }

            hideToolbar();
          })
          .catch((err) => {
            // Remove loading bubble on error
            if (loadingBubble && loadingBubble.parentNode) {
              loadingBubble.classList.add("closing");
              setTimeout(() => {
                if (loadingBubble.parentNode)
                  loadingBubble.parentNode.removeChild(loadingBubble);
              }, 200);
            }

            console.error(err);
            if (window.Swal)
              window.Swal.fire(
                "Network error",
                "Failed to fetch translation. Check network or CORS.",
                "error"
              );
          });
      });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      waitForRenditionAndInit();
    });
  } else {
    // already loaded
    waitForRenditionAndInit();
  }
})();

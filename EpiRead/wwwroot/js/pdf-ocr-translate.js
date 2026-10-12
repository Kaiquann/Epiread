(function () {
  // Client-side PDF image OCR + translate flow using Tesseract.js and server proxy
  // Expects a single <canvas> element inside #pdf-viewer (the code in _PdfReaderPartial creates one)

  /**
   * Creates an enhanced translation bubble with better design and UX
   * @param {string} translation - The translated text
   * @param {string} originalText - The original OCR text
   * @param {DOMRect} containerRect - Canvas bounding rectangle
   * @param {string} fromLang - Source language code
   * @param {string} toLang - Target language code
   * @returns {HTMLElement} The bubble element
   */
  function createBubble(
    translation,
    originalText,
    containerRect,
    fromLang = "auto",
    toLang = "EN"
  ) {
    // remove existing bubble with animation
    const existing = document.querySelector(".pdf-translation-bubble");
    if (existing && existing.parentNode) {
      existing.classList.add("closing");
      setTimeout(() => {
        if (existing.parentNode) existing.parentNode.removeChild(existing);
      }, 200);
    }

    const bubble = document.createElement("div");
    bubble.className = "pdf-translation-bubble translation-bubble";

    // Build the enhanced bubble structure
    bubble.innerHTML = `
      <div class="bubble-header">
        <div class="bubble-lang-info">
          <span class="bubble-lang-badge">${escapeHtml(
            getLanguageName(fromLang)
          )}</span>
          <i class="bi bi-arrow-right bubble-arrow"></i>
          <span class="bubble-lang-badge">${escapeHtml(
            getLanguageName(toLang)
          )}</span>
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

    // Append to body for better positioning
    document.body.appendChild(bubble);

    // Position the bubble
    try {
      // Wait for next frame to get accurate dimensions
      requestAnimationFrame(() => {
        const bw = bubble.offsetWidth || 320;
        const bh = bubble.offsetHeight || 200;

        // Try to center horizontally relative to canvas
        let left = Math.round(
          containerRect.left + containerRect.width / 2 - bw / 2
        );
        let top = Math.round(containerRect.top + 16);

        // Clamp to viewport
        const vw = window.innerWidth || document.documentElement.clientWidth;
        const vh = window.innerHeight || document.documentElement.clientHeight;

        left = Math.max(16, Math.min(left, vw - bw - 16));
        top = Math.max(16, Math.min(top, vh - bh - 16));

        bubble.style.left = left + "px";
        bubble.style.top = top + "px";
      });
    } catch (e) {
      console.error("Error positioning bubble:", e);
    }

    // Event handlers
    const closeBtn = bubble.querySelector(".bubble-close");
    if (closeBtn) {
      closeBtn.addEventListener("click", () => {
        bubble.classList.add("closing");
        setTimeout(() => {
          if (bubble.parentNode) bubble.parentNode.removeChild(bubble);
        }, 200);
      });
    }

    // Copy button handlers
    const copyButtons = bubble.querySelectorAll(".bubble-action-btn");
    copyButtons.forEach((btn) => {
      btn.addEventListener("click", async function () {
        const action = this.getAttribute("data-action");
        let textToCopy = "";

        if (action === "copy-original") {
          textToCopy = originalText;
        } else if (action === "copy-translation") {
          textToCopy = translation;
        }

        try {
          await navigator.clipboard.writeText(textToCopy);

          // Visual feedback
          const originalHTML = this.innerHTML;
          this.classList.add("copied");
          this.innerHTML =
            '<i class="bi bi-check-circle"></i><span>Copied!</span>';

          setTimeout(() => {
            this.classList.remove("copied");
            this.innerHTML = originalHTML;
          }, 2000);
        } catch (err) {
          console.error("Failed to copy text:", err);
          // Fallback for older browsers
          const textArea = document.createElement("textarea");
          textArea.value = textToCopy;
          textArea.style.position = "fixed";
          textArea.style.left = "-999999px";
          document.body.appendChild(textArea);
          textArea.select();
          try {
            document.execCommand("copy");
            const originalHTML = this.innerHTML;
            this.classList.add("copied");
            this.innerHTML =
              '<i class="bi bi-check-circle"></i><span>Copied!</span>';
            setTimeout(() => {
              this.classList.remove("copied");
              this.innerHTML = originalHTML;
            }, 2000);
          } catch (e) {
            console.error("Fallback copy failed:", e);
          }
          document.body.removeChild(textArea);
        }
      });
    });

    return bubble;
  }

  /**
   * Creates a loading bubble
   * @param {DOMRect} containerRect - Canvas bounding rectangle
   * @returns {HTMLElement} The loading bubble element
   */
  function createLoadingBubble(containerRect) {
    const existing = document.querySelector(".pdf-translation-bubble");
    if (existing && existing.parentNode) {
      existing.classList.add("closing");
      setTimeout(() => {
        if (existing.parentNode) existing.parentNode.removeChild(existing);
      }, 200);
    }

    const bubble = document.createElement("div");
    bubble.className = "pdf-translation-bubble translation-bubble";
    bubble.innerHTML = `
      <div class="bubble-loading">
        <div class="bubble-spinner"></div>
        <div class="bubble-loading-text">Processing OCR & Translation...</div>
      </div>
    `;

    document.body.appendChild(bubble);

    // Position
    try {
      requestAnimationFrame(() => {
        const bw = bubble.offsetWidth || 320;
        const bh = bubble.offsetHeight || 150;
        let left = Math.round(
          containerRect.left + containerRect.width / 2 - bw / 2
        );
        let top = Math.round(containerRect.top + 16);
        const vw = window.innerWidth || document.documentElement.clientWidth;
        const vh = window.innerHeight || document.documentElement.clientHeight;
        left = Math.max(16, Math.min(left, vw - bw - 16));
        top = Math.max(16, Math.min(top, vh - bh - 16));
        bubble.style.left = left + "px";
        bubble.style.top = top + "px";
      });
    } catch (e) {
      console.error("Error positioning loading bubble:", e);
    }

    return bubble;
  }

  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
  }

  // Helper: Get language name from code (mirrors LanguageMappings.cs)
  function getLanguageName(code) {
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
    const base = (code || "").split(/[-_]/)[0].toLowerCase();
    return langMap[base] || code.toUpperCase();
  }

  function showSpinnerOnBtn(btn, show) {
    if (!btn) return;
    if (show) {
      btn.dataset.orig = btn.textContent;
      btn.textContent = "Working...";
      btn.setAttribute("disabled", "disabled");
    } else {
      if (btn.dataset.orig) btn.textContent = btn.dataset.orig;
      btn.removeAttribute("disabled");
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    const ocrBtn = document.getElementById("pdfOcrBtn");
    const fromSel = document.getElementById("pdfFromLang");
    const toSel = document.getElementById("pdfToLang");

    if (!ocrBtn) return;

    let worker = null;
    let recognizing = false;

    // Track if user explicitly changed the source language -- leave 'auto' alone otherwise
    try {
      if (fromSel) {
        fromSel.addEventListener("change", function () {
          if (this.value && this.value.toLowerCase() === "auto") {
            delete this.dataset.userSelected;
          } else {
            this.dataset.userSelected = "true";
          }
        });
      }
    } catch (e) {
      /* ignore */
    }

    async function ensureWorker() {
      if (worker) return worker;
      try {
        // Allow server to inject explicit paths to avoid version/path mismatches
        // (injected in Views/Shared/Translate/_PdfTools.cshtml as window._tesseractConfig)
        const tconfig = window._tesseractConfig || {};

        // Configure worker options with custom paths if provided
        // Note: In Tesseract.js v5+, createWorker already returns a fully initialized worker
        const workerOptions = {
          workerPath: tconfig.workerPath || undefined,
          corePath: tconfig.corePath || undefined,
          langPath: tconfig.langPath || undefined,
          workerBlobURL: tconfig.workerBlobURL || undefined,
        };

        // Remove undefined values to avoid issues
        Object.keys(workerOptions).forEach(
          (key) => workerOptions[key] === undefined && delete workerOptions[key]
        );

        // In Tesseract.js v5+, createWorker returns a Promise that resolves to a ready-to-use worker
        // No need to call load(), loadLanguage(), or initialize() separately
        // Signature: createWorker(langs, oem, options)
        // Default to English for OCR; can reinitialize later if needed
        worker = await Tesseract.createWorker("eng", 1, workerOptions);

        console.log("Tesseract worker initialized successfully");
      } catch (e) {
        console.error("Failed to init Tesseract worker", e);
        worker = null;
      }
      return worker;
    }

    ocrBtn.addEventListener("click", async function () {
      if (recognizing) return;
      const canvas = document.querySelector("#pdf-viewer canvas");
      if (!canvas) {
        Swal.fire &&
          Swal.fire("No canvas", "PDF canvas not found for OCR.", "warning");
        return;
      }

      const containerRect = canvas.getBoundingClientRect();

      showSpinnerOnBtn(ocrBtn, true);
      recognizing = true;

      // Reset source language to 'auto' UNLESS user explicitly set a language
      // This ensures each OCR/translation starts fresh with auto-detection
      if (fromSel && !fromSel.dataset.userSelected) {
        fromSel.value = "auto";
      }

      // Show loading bubble
      createLoadingBubble(containerRect);

      try {
        const w = await ensureWorker();
        if (!w) {
          throw new Error("OCR worker unavailable");
        }

        // Use the canvas element directly
        const {
          data: { text },
        } = await w.recognize(canvas);

        if (!text || !text.trim()) {
          // Remove loading bubble
          const loadingBubble = document.querySelector(
            ".pdf-translation-bubble"
          );
          if (loadingBubble && loadingBubble.parentNode) {
            loadingBubble.classList.add("closing");
            setTimeout(() => {
              if (loadingBubble.parentNode)
                loadingBubble.parentNode.removeChild(loadingBubble);
            }, 200);
          }

          Swal.fire &&
            Swal.fire(
              "No text found",
              "No OCR text detected on this page. Try zooming in or selecting a clearer area.",
              "info"
            );
          showSpinnerOnBtn(ocrBtn, false);
          recognizing = false;
          return;
        }

        // send to translate proxy
        const sl = fromSel && fromSel.value ? fromSel.value : "auto";
        const tl = toSel && toSel.value ? toSel.value : "EN";
        const payload = { text: text, sl: sl, tl: tl };

        const resp = await fetch("/Translate/TranslateText", {
          method: "POST",
          headers: { "Content-Type": "application/x-www-form-urlencoded" },
          body: new URLSearchParams(payload),
        });

        if (!resp.ok) {
          const msg = await resp.text();
          throw new Error("Translate failed: " + resp.status + " " + msg);
        }

        const data = await resp.json();
        // parse Google translate response: data[0] is array of segments
        let translation = "";
        let detectedLang = sl;

        try {
          if (Array.isArray(data) && Array.isArray(data[0])) {
            translation = data[0].map((p) => p[0]).join("");
          }
          // Try to get detected language if auto-detect was used
          if (sl === "auto" && Array.isArray(data) && data[2]) {
            detectedLang = data[2].toUpperCase();
          }
        } catch (e) {
          console.error("Error parsing translation:", e);
        }

        if (!translation) {
          // Remove loading bubble
          const loadingBubble = document.querySelector(
            ".pdf-translation-bubble"
          );
          if (loadingBubble && loadingBubble.parentNode) {
            loadingBubble.classList.add("closing");
            setTimeout(() => {
              if (loadingBubble.parentNode)
                loadingBubble.parentNode.removeChild(loadingBubble);
            }, 200);
          }

          Swal.fire &&
            Swal.fire(
              "Translation error",
              "Could not parse translation response.",
              "error"
            );
          showSpinnerOnBtn(ocrBtn, false);
          recognizing = false;
          return;
        }

        // Create the enhanced translation bubble
        createBubble(
          translation,
          text.trim(),
          containerRect,
          detectedLang.toUpperCase(),
          tl.toUpperCase()
        );
      } catch (err) {
        console.error("OCR/Translation error:", err);

        // Remove loading bubble and show error
        const loadingBubble = document.querySelector(".pdf-translation-bubble");
        if (loadingBubble && loadingBubble.parentNode) {
          loadingBubble.classList.add("closing");
          setTimeout(() => {
            if (loadingBubble.parentNode)
              loadingBubble.parentNode.removeChild(loadingBubble);
          }, 200);
        }

        Swal.fire &&
          Swal.fire(
            "Error",
            "OCR / Translation failed. " +
              (err.message || "See console for details."),
            "error"
          );
      } finally {
        showSpinnerOnBtn(ocrBtn, false);
        recognizing = false;
      }
    });

    // cleanup bubbles when clicking outside or when navigating pages/zooming
    document.addEventListener("click", function (e) {
      // if clicked outside bubble, remove it with animation
      const b = document.querySelector(".pdf-translation-bubble");
      if (!b) return;
      if (!b.contains(e.target) && !ocrBtn.contains(e.target)) {
        b.classList.add("closing");
        setTimeout(() => {
          if (b.parentNode) b.parentNode.removeChild(b);
        }, 200);
      }
    });
  });
})();

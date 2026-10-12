// ============================================================================
// UnCategorized Section
// ============================================================================

// Client-side unobtrusive adapter for PublicationYearRangeAttribute (maps to jQuery 'range')
// This ensures client-side validation using the same min/max values produced on the server.
(function ($) {
  if ($.validator && $.validator.unobtrusive) {
    // Add adapter for PublicationYearRangeAttribute: supports min, max, and optional url to validate remotely via AJAX
    $.validator.unobtrusive.adapters.add(
      "publicationyearrange",
      ["min", "max", "url"],
      function (options) {
        // Parse integer boundaries (fallback to NaN if not provided)
        var min = parseInt(options.params.min);
        var max = parseInt(options.params.max);
        const url = options.params.url;

        // If a remote url is provided, use the "remote" rule to validate on server via AJAX
        if (url) {
          // Build data to be sent; value is taken from the form element at validation time
          options.rules["remote"] = {
            url: url,
            type: "GET", // or 'POST' if you prefer; adapt server accordingly
            // Send control value and boundaries; server should accept these names
            data: {
              value: function () {
                return $(options.form)
                  .find(`:input[name='${options.element.name}']`)
                  .val();
              },
              min: function () {
                return isNaN(min) ? null : min;
              },
              max: function () {
                return isNaN(max) ? null : max;
              },
            },
          };
          // Use same message for remote rule
          options.messages["remote"] = options.message;
        } else {
          // If no url, fallback to standard range validator (client-only)
          options.rules["range"] = [min, max];
          options.messages["range"] = options.message;
        }
      }
    );
  }
})(jQuery);

// Initiate GET request (AJAX-supported)
$(document).on("click", "[data-get]", (e) => {
  e.preventDefault();
  const url = e.target.dataset.get;
  location = url || location;
});

// Confirmation message (AJAX-supported)
$(document).on("click", "[data-confirm]", (e) => {
  const text = e.target.dataset.confirm || "Are you sure?";
  if (!confirm(text)) {
    e.preventDefault();
    e.stopImmediatePropagation();
  }
});

// Initiate POST request (AJAX-supported)
$(document).on("click", "[data-post]", (e) => {
  e.preventDefault();
  const url = e.target.dataset.post;
  const f = $("<form>").appendTo(document.body)[0];
  f.method = "POST";
  f.action = url || location;
  f.submit();
});

// Search bar (AJAX-supported)
let timer = null;
$(document).on("input", "#search", (e) => {
  clearTimeout(timer);
  timer = setTimeout(() => $(e.target.form).submit(), 500);
});

// Trim input
$("[data-trim]").on("change", (e) => {
  e.target.value = e.target.value.trim();
});

// Auto uppercase
$("[data-upper]").on("input", (e) => {
  const a = e.target.selectionStart;
  const b = e.target.selectionEnd;
  e.target.value = e.target.value.toUpperCase();
  e.target.setSelectionRange(a, b);
});

// RESET form
$("[type=reset]").on("click", (e) => {
  e.preventDefault();
  location = location;
});

// Autofocus
$("form :input:not(button):first").focus();
$(".err:first").prev().focus();
$(".err:first").prev().find(":input:first").focus();

//show the toggle button content
$("[toggle-button]").on("click", (e) => {
  $("#dropdown-content").toggleClass("show");
});

// decrease quantity value
$("[decrease]").on("click", (e) => {
  const value = parseInt($(".quantity").val());
  if (value > 1) {
    $(".quantity").val(value - 1);
  }
});

//increase quantity value
$("[increase]").on("click", (e) => {
  const value = parseInt($(".quantity").val());
  if (value > 0) {
    $(".quantity").val(value + 1);
  }
});

/**
 * @author: Chong Jun Xiang
 * @description:
 * Manages the AJAX loading indicator.
 * - Adds the 'show' class to the loader element when an AJAX request starts.
 * - Removes the 'show' class from the loader element when an AJAX request completes.
 */
$(document)
  .ajaxStart(function () {
    $("#loader").addClass("show");
  })
  .ajaxStop(function () {
    $("#loader").removeClass("show");
  });

/**
 * @author: Chong Jun Xiang
 * @description: Global AJAX success handler for redirect responses
 * Handles JSON responses that contain a redirect URL and performs page navigation
 */
$(document).ajaxSuccess(function (event, xhr, settings) {
  // Check if response is JSON and contains redirect property
  try {
    const response = JSON.parse(xhr.responseText);
    if (response && response.redirect) {
      window.location.href = response.redirect;
    }
  } catch (e) {
    // Not JSON or doesn't contain redirect, continue normal processing
  }
});

/**
 * @author: Chong Jun Xiang
 * @description: Global AJAX complete handler to reinitialize validation
 * When AJAX updates HTML content containing forms with validation attributes,
 * this handler ensures that client-side validation is reinitialized for the new content.
 * This fixes the issue where Remote validation attributes stop working after AJAX partial updates.
 */
$(document).ajaxComplete(function (event, xhr, settings) {
  // Only process HTML responses (skip JSON responses)
  const contentType = xhr.getResponseHeader("Content-Type") || "";
  if (contentType.indexOf("html") > -1) {
    // Find all forms in the document and reinitialize validation
    $("form").each(function () {
      const $form = $(this);
      
      // Remove existing validation
      const validator = $form.data("validator");
      if (validator) {
        validator.destroy();
      }
      $form.removeData("validator");
      $form.removeData("unobtrusiveValidation");
      
      // Reinitialize validation only if the unobtrusive plugin exists
      // (some pages / builds may not include jquery.validate or the unobtrusive adapter)
      if ($.validator && $.validator.unobtrusive && typeof $.validator.unobtrusive.parse === "function") {
        $.validator.unobtrusive.parse($form);
      }
    });
  }
});

/**
 * @author: Chong Jun Xiang
 * @description:
 * Toggles the visibility of the file input field based on the checkbox state.
 * - Shows the file input field and sets the value to "1" when the checkbox is checked.
 * - Hides the file input field and sets the value to "0" when the checkbox is unchecked.
 */
$('input[name="update_image"]').on("change", function () {
  if ($(this).is(":checked")) {
    $("#update_file_image").show();
    $("#update_image").val("1");
  } else {
    $("#update_file_image").hide();
    $("#update_image").val("0");
  }
});

// ============================================================================
// Table Section
// ============================================================================

/**
 * @author: Chong Jun Xiang
 * @description:
 * Handles the click event on elements with the 'data-checkable' attribute.
 * - Supports regular click for selecting a checkbox.
 * - Supports Ctrl + Click for selecting multiple checkboxes.
 * - Supports Ctrl + Shift + Click for selecting a range of checkboxes.
 */
let lastCheckedIndex = null;

$(document).on("click", "[data-checkable]", (e) => {
  if ($(e.target).is(":input,a")) return;

  const $rows = $("[data-checkable]");
  const currentIndex = $rows.index(e.currentTarget);

  if (e.ctrlKey && e.shiftKey && lastCheckedIndex !== null) {
    // Handle Ctrl + Shift + Click
    const start = Math.min(lastCheckedIndex, currentIndex);
    const end = Math.max(lastCheckedIndex, currentIndex);

    for (let i = start; i <= end; i++) {
      $rows.eq(i).find(":checkbox").prop("checked", true).trigger("change");
    }
  } else {
    // Handle regular click and Ctrl + Click
    $(e.currentTarget)
      .find(":checkbox")
      .prop("checked", (i, v) => !v)
      .trigger("change");
  }

  lastCheckedIndex = currentIndex;
});

/**
 * @description:
 * Manages click events for elements with the 'select-submit-btn' class.
 * - Displays a customizable confirmation dialog to the user.
 * - Upon confirmation, submits the form with the specified action URL.
 * - If the button has data-ajax="true", submits the form via AJAX.
 * - If the button has data-ajax-update, updates the specified part of the page with the response.
 */
$(document).on("click", ".select-submit-btn", (e) => {
  const message = $(e.target).data("message");
  const action = $(e.target).data("action");
  const isAjax = $(e.target).data("ajax") === true;
  const updateTarget = $(e.target).data("ajax-update");

  if (!confirm(message)) {
    return;
  }

  if (isAjax) {
    e.preventDefault();
    $.ajax({
      type: "POST",
      url: action,
      data: $("#select-form").serialize(),
      success: function (response) {
        // Handle success response
        console.log("Form submitted successfully via AJAX");
        if (updateTarget) {
          $(updateTarget).html(response);
        }
      },
      error: function (xhr, status, error) {
        // Handle error response
        console.error("Error submitting form via AJAX:", error);
      },
    });
  } else {
    $("#select-form").attr("action", action).submit();
  }
});

/**
 * @author: Chong Jun Xiang
 * @description:
 * Handles the change event on elements with the 'select-box-all' class.
 * - Toggles the checked state of all checkboxes with the 'select-box' class.
 */
$(document).on("change", ".select-box-all", (e) => {
  const isChecked = $(e.target).is(":checked");
  $(".select-box").prop("checked", isChecked);
});

/**
 * @author: Chong Jun Xiang
 * @description:
 * Handles the keydown event to support Ctrl + A hotkey for selecting all checkboxes.
 * - Toggles the checked state of all checkboxes with the 'select-box' class.
 * - Prevents the default 'select all' browser behavior.
 * - Ignores the event if the search box is focused.
 */
let allChecked = false; // Flag to track checkbox state
$(document).on("keydown", function (e) {
  if (
    $("#select-form").length > 0 &&
    e.ctrlKey &&
    e.key === "a" &&
    $(".select-box").length > 0
  ) {
    if ($(e.target).is("#search")) {
      return; // Do nothing if the search box is focused
    }
    e.preventDefault(); // Prevent default 'select all' browser behavior
    allChecked = !allChecked; // Toggle the flag
    $(".select-box").prop("checked", allChecked);
    $(".select-box-all").prop("checked", allChecked);
  }
});

/**
 * @author: Chong Jun Xiang
 * @description:
 * Handles the change event for the filter dropdown.
 * - Automatically submits the form when the filter selection changes.
 */
$(document).on("change", 'select[name="filter"]', (e) => {
  $("#filter-form").submit();
});

// ============================================================================
// Drag and Drop Section
// ============================================================================
/**
 * @author: Chong Jun Xiang
 * @description:
 * Implements drag and drop functionality for file uploads.
 * - Allows users to drag files over a designated drop zone and drop them to upload.
 * - Highlights the drop zone when a file is dragged over it.
 * - Automatically submits the form when files are selected or dropped.
 */
$("[allow-drag-drop]").each(function () {
  const container = $(this);
  const input = container.find("input[type=file]");
  const dropzone = container.find(".dropzone");

  dropzone.on("dragover", (e) => {
    e.preventDefault();
    dropzone.addClass("dragover");
  });

  dropzone.on("dragleave", () => {
    dropzone.removeClass("dragover");
  });

  dropzone.on("drop", (e) => {
    e.preventDefault();
    dropzone.removeClass("dragover");
    input.prop("files", e.originalEvent.dataTransfer.files);
  });

  input.on("change", () => {
    container.find("form").submit();
  });
});

// ============================================================================
// Everything printable Section
// ============================================================================
/**
 * @author: Chong Jun Xiang
 * @description:
 * Dynamically prints the content of a specified area when a print button is clicked.
 * - The button should have a 'data-print-id' attribute.
 * - The corresponding printable area should have a 'data-printable-id' attribute with the same value.
 * - Generates an iframe, loads the printable content into it with necessary styles.
 * - Triggers the print dialog.
 * - Removes the iframe from the DOM after printing.
 */
$(document).on("click", ".print_btn", function (e) {
  e.preventDefault();

  // Get the print ID from the clicked button
  const printId = $(this).data("print-id");

  // Find the printable element with the matching data-printable-id
  const htmlPrintable = $(`[data-printable-id="${printId}"]`).html();

  if (!htmlPrintable) {
    alert("No printable content found!");
    return;
  }

  // Create a new iframe for printing
  const iframe = document.createElement("iframe");
  iframe.style.position = "absolute";
  iframe.style.width = "0";
  iframe.style.height = "0";
  iframe.style.border = "none";
  document.body.appendChild(iframe);

  const doc = iframe.contentWindow.document;
  doc.open();
  doc.write(`
        <!DOCTYPE html>
        <html>
            <head>
                <title></title>
                <link rel="stylesheet" href="/css/app.css">
                <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/bootstrap/5.3.8/css/bootstrap.min.css">
                <style>
                    body {
                        margin: 20px;
                    }
                </style>
            </head>
            <body>
                ${htmlPrintable}
            </body>
        </html>
    `);
  doc.close();

  iframe.contentWindow.onload = function () {
    iframe.contentWindow.print();
    document.body.removeChild(iframe);
  };
});

// ============================================================================
// Photo Preview Section
// ============================================================================
/**
 * @author: Chong Jun Xiang
 * @description:
 * Combines drag and drop functionality with a file input field for uploading images.
 * - Supports multiple image file uploads.
 * - Displays a preview of the selected images.
 * - Limits the number of files to a maximum of 5.
 * - Provides visual feedback during drag and drop operations.
 */
function preview(e, container, img, isMultiple) {
  const files = e.target.files;
  const validFiles = [];
  const invalidFiles = [];
  for (const file of files) {
    if (file.type.startsWith("image/")) {
      validFiles.push(file);
    } else {
      invalidFiles.push(file.name);
    }
  }

  if (invalidFiles.length > 0) {
    return alert(`Invalid files: ${invalidFiles.join(", ")}`);
  }

  if (validFiles.length > 5) {
    return alert("Only allowed maximum of 5 files");
  }

  if (!isMultiple) {
    // Single file upload: directly change the current image preview
    const file = validFiles[0];
    if (file) {
      const objectUrl = URL.createObjectURL(file);
      img.onload = () => URL.revokeObjectURL(objectUrl);
      img.src = objectUrl;
      container.find("span").text("1 file selected");
    } else {
      img.src = img.dataset.src;
      container.find("span").text("Drag & Drop file here");
    }
  } else {
    // Multiple file upload: append new images
    container.find("img:not(:first-of-type)").remove();
    if (validFiles.length > 0) {
      validFiles.forEach((file) => {
        const reader = new FileReader();
        reader.onload = (event) => {
          $(img).after(
            `<img src="${event.target.result}" alt="Uploaded Image">`
          );
        };
        reader.onerror = () => alert("Error reading file");
        reader.readAsDataURL(file);
      });
      container.find("span").text(`${validFiles.length} file(s) selected`);
    } else {
      img.src = img.dataset.src;
      container.find("span").text("Drag & Drop file here");
    }
  }
}

$(document).on("change", "label.upload input[type=file]", function (e) {
  const container = $(this).closest("label.upload");
  const img = container.find("img")[0];
  const input = container.find("input[type=file]")[0];
  const isMultiple = input.multiple;

  preview(e, container, img, isMultiple);
});

$(document).on("dragover", "label.upload", function (e) {
  e.preventDefault();
  const container = $(this);
  container.addClass("dragover");
  container.find("span").text("Drop file here");
});

$(document).on("dragleave", "label.upload", function () {
  const container = $(this);
  container.removeClass("dragover");
  container.find("span").text("Drag & Drop file here");
});

$(document).on("drop", "label.upload", function (e) {
  e.preventDefault();
  const container = $(this);
  const input = container.find("input[type=file]")[0];
  const img = container.find("img")[0];
  const isMultiple = input.multiple;

  container.removeClass("dragover");
  const files = e.originalEvent.dataTransfer.files;
  if (!isMultiple && files.length > 1) {
    return alert("Only allowed maximum of 1 file");
  }
  if (files.length > 0) {
    input.files = files;
    preview({ target: { files } }, container, img, isMultiple); // Create a mock event object
  } else {
    console.error("No files dropped");
  }
});

// ============================================================================
// QR Code Section
// ============================================================================
/**
 * @description:
 * Initializes and handles QR code scanning functionality.
 * - Binds a click event to elements with the 'data-qrcode' attribute.
 * - Initializes the QR code scanner if not already initialized.
 * - Renders the QR code scanner in the specified target element.
 * - Handles successful QR code scans by displaying the result and optionally opening URLs.
 * - Handles scan failures by logging warnings.
 *
 * Usage:
 * <div id="reader"></div>
 * <div id="scan-result"></div>
 * <button data-qrcode>Start QR Scanner</button>
 */
let html5QrcodeScanner = null;

$(document).on("click", "[data-qrcode]", function () {
  $("#scan-result").text("");
  html5QrcodeScanner = new Html5QrcodeScanner(
    "reader",
    { fps: 60, qrbox: { width: 250, height: 250 } },
    false
  );
  html5QrcodeScanner.render(onScanSuccess, onScanFailure);
});

function onScanSuccess(decodedText, decodedResult) {
  //console.log(`Code matched = ${decodedText}`, decodedResult);
  $("#scan-result").text(`Scanned Result: ${decodedText}`);

  // Clear the scanner
  html5QrcodeScanner
    .clear()
    .catch((err) => console.error("Error clearing scanner", err));

  // Handle HTTP links
  if (decodedText.startsWith("http")) {
    const userConfirmed = confirm(
      `The QR code contains a link: ${decodedText}. Do you want to open it?`
    );
    if (userConfirmed) {
      window.open(decodedText, "_blank");
    }
  } else {
    alert(`QR Code Scanned: ${decodedText}`);
  }
}

function onScanFailure(error) {
  console.warn(`Code scan error = ${error}`);
}

// ============================================================================
// Slider Section
// ============================================================================
/**
 * @author: Kai Quan
 * @description: product will swap automative
 */
let $slides = $(".slider-container img");
let $slideButton = $(".slider-container a");
let $dots = $(".dots-container .dot");
let currentSlide = 0;

function showNextSlide() {
  $slides.eq(currentSlide).removeClass("active").addClass("inactive");
  $slideButton.eq(currentSlide).removeClass("active").addClass("inactive");
  $dots.eq(currentSlide).removeClass("active-dot");

  currentSlide = (currentSlide + 1) % $slides.length;

  $slides.eq(currentSlide).removeClass("inactive").addClass("active");
  $slideButton.eq(currentSlide).removeClass("inactive").addClass("active");
  $dots.eq(currentSlide).addClass("active-dot");
}

setInterval(showNextSlide, 2000);

/**
 * @author: Kai Quan
 * @description: when user click the product slider button will swap the image
 */
$dots.on("click", function () {
  const slideIndex = $(this).data("slide");
  $slides.eq(currentSlide).removeClass("active").addClass("inactive");
  $slideButton.eq(currentSlide).removeClass("active").addClass("inactive");
  $dots.eq(currentSlide).removeClass("active-dot");
  currentSlide = slideIndex;
  $slides.eq(currentSlide).removeClass("inactive").addClass("active");
  $slideButton.eq(currentSlide).removeClass("inactive").addClass("active");
  $dots.eq(currentSlide).addClass("active-dot");
});

// ============================================================================
// DropDown Toggle Section
// ============================================================================
/**
 * @author: Kai Quan
 * @description: when user click the product slider button will swap the image
 */
const $dropdownToggle = $("#dropdownToggle");
const $dropdownMenu = $("#dropdownMenu");

$dropdownToggle.on("click", function (e) {
  e.stopPropagation(); // Prevent event from bubbling up
  $dropdownMenu.toggleClass("show");
});

// Hide menu
$(document).on("click", function () {
  $dropdownMenu.removeClass("show");
});

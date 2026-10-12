// ============================================================================
// AI Description Generation Section
// ============================================================================

/**
 * @author: Chong Jun Xiang
 * @description:
 * State management for the AI description generation modal.
 * Tracks modal visibility, generation state, and user actions.
 */
const aiDescriptionModalState = {
    isOpen: false,
    isGenerating: false,
    currentDescription: '',
    generatedDescription: '',
    bookId: null,
    abortController: null,

    /**
     * Open the modal and display current description
     */
    open(bookId, currentDescription) {
        this.bookId = bookId;
        this.currentDescription = currentDescription;
        this.generatedDescription = '';
        this.isOpen = true;
        this.isGenerating = false;

        // Show backdrop and modal with animation
        $('#ai-description-modal-backdrop').fadeIn(300);
        $('#ai-description-modal').fadeIn(300);

        // Disable body scroll
        $('body').css('overflow', 'hidden');

        // Display current description
        $('#current-description-display').text(currentDescription || '(No description yet)');
        $('#generated-description-display').html('');
        $('#generation-loading').hide();

        // Enable buttons
        this.updateButtonStates();
    },

    /**
     * Close the modal with optional confirmation
     */
    close(requiresConfirmation = false) {
        if (requiresConfirmation && this.isGenerating) {
            // Show confirmation before closing while generating
            // Use arrow function to preserve 'this' context
            Swal.fire({
                title: 'Cancel Generation?',
                text: 'The AI is still generating. Are you sure you want to cancel?',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Yes, Cancel',
                cancelButtonText: 'No, Continue',
                confirmButtonColor: '#dc3545',
                cancelButtonColor: '#6c757d'
            }).then((result) => {
                if (result.isConfirmed) {
                    aiDescriptionModalState.abort(); // Use direct reference instead of this
                }
            });
        } else {
            this.abort();
        }
    },

    /**
     * Actually perform the close action
     */
    abort() {
        // Cancel any ongoing AJAX request
        if (this.abortController) {
            console.log('Aborting AJAX request...');
            this.abortController.abort();
            this.abortController = null;
        }

        this.isOpen = false;
        this.isGenerating = false;

        // Hide backdrop and modal with animation
        $('#ai-description-modal-backdrop').fadeOut(300);
        $('#ai-description-modal').fadeOut(300, function() {
            // Show toast or brief notification that generation was cancelled
            if (aiDescriptionModalState.isGenerating === false && aiDescriptionModalState.bookId !== null) {
                console.log('Generation cancelled successfully');
            }
        });

        // Restore body scroll
        $('body').css('overflow', 'auto');

        // Reset state
        this.currentDescription = '';
        this.generatedDescription = '';
        this.bookId = null;
    },

    /**
     * Update button states based on modal state
     */
    updateButtonStates() {
        const $cancelBtn = $('#ai-modal-cancel-btn');
        const $regenerateBtn = $('#ai-modal-regenerate-btn');
        const $acceptBtn = $('#ai-modal-accept-btn');

        if (this.isGenerating) {
            $cancelBtn.prop('disabled', false);
            $regenerateBtn.prop('disabled', true);
            $acceptBtn.prop('disabled', true);
        } else {
            $cancelBtn.prop('disabled', false);
            $regenerateBtn.prop('disabled', !this.generatedDescription);
            $acceptBtn.prop('disabled', !this.generatedDescription);
        }
    },

    /**
     * Set loading state for generation
     */
    setLoading(isLoading) {
        this.isGenerating = isLoading;
        if (isLoading) {
            $('#generation-loading').show();
            $('#generated-description-display').hide();
        } else {
            $('#generation-loading').hide();
            $('#generated-description-display').show();
        }
        this.updateButtonStates();
    },

    /**
     * Display generated description
     */
    displayGenerated(text) {
        this.generatedDescription = text;
        $('#generated-description-display').text(text);
        this.updateButtonStates();
    },

    /**
     * Replace description in main form with confirmation
     */
    acceptAndReplace() {
        if (this.generatedDescription) {
            // Show confirmation dialog before replacing
            Swal.fire({
                title: 'Confirm Replace?',
                html: `
                    <div class="text-start" style="max-height: 300px; overflow-y: auto;">
                        <p><strong>Current Description:</strong></p>
                        <p style="padding: 10px; background-color: #f8f9fa; border-radius: 4px; margin-bottom: 15px;">
                            ${this.currentDescription || '(No description)'}
                        </p>
                        <p><strong>Will be replaced with:</strong></p>
                        <p style="padding: 10px; background-color: #e8f5e9; border-radius: 4px;">
                            ${this.generatedDescription}
                        </p>
                    </div>
                `,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Yes, Replace',
                cancelButtonText: 'No, Keep Current',
                confirmButtonColor: '#28a745',
                cancelButtonColor: '#6c757d'
            }).then((result) => {
                if (result.isConfirmed) {
                    // User confirmed, replace the description
                    $('textarea[name="Description"]').val(this.generatedDescription).trigger('change');
                    this.abort();

                    Swal.fire({
                        icon: 'success',
                        title: 'Success',
                        text: 'Description has been replaced with the AI-generated one.',
                        timer: 2000
                    });
                }
                // If cancelled, do nothing (modal stays open, user can regenerate or cancel)
            });
        }
    }
};

/**
 * @description:
 * Handles AI-powered book description generation with floating modal UI.
 * - Shows modal with current description
 * - Sends book ID to backend via AJAX
 * - Displays generated description in modal
 * - Allows user to regenerate, accept, or cancel
 * - Prevents background interaction while modal is open
 */
$(document).on('click', '#ai-generate-description-btn', async function (e) {
    e.preventDefault();

    // Get the book ID from the Edit form
    const bookId = $('input[name="Id"]').val();
    const currentDescription = $('textarea[name="Description"]').val() || '';

    if (!bookId || bookId <= 0) {
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'Invalid book ID. Please save the book first.'
        });
        return;
    }

    // Open modal and show current description
    aiDescriptionModalState.open(bookId, currentDescription);

    // Automatically start generation
    await generateDescription(bookId);
});

/**
 * Get the appropriate endpoint based on the current page location
 */
function getGenerateDescriptionEndpoint(bookId) {
    // Check if we're in Admin or Author area by looking at the current URL
    const currentUrl = window.location.pathname.toLowerCase();
    
    if (currentUrl.includes('/admin/')) {
        return `/Admin/Book/GenerateDescription?id=${bookId}`;
    } else if (currentUrl.includes('/author/')) {
        return `/Author/Book/GenerateDescription?id=${bookId}`;
    } else {
        // Default to Admin for backward compatibility
        return `/Admin/Book/GenerateDescription?id=${bookId}`;
    }
}

/**
 * Generate description via API call with proper abort support
 */
async function generateDescription(bookId) {
    aiDescriptionModalState.setLoading(true);

    try {
        // Create abort controller for this request
        aiDescriptionModalState.abortController = new AbortController();
        const signal = aiDescriptionModalState.abortController.signal;

        // Determine the correct endpoint
        const endpoint = getGenerateDescriptionEndpoint(bookId);

        // Use Fetch API instead of jQuery AJAX for proper AbortController support
        const response = await fetch(endpoint, {
            method: 'POST',
            contentType: 'application/json',
            headers: {
                'Content-Type': 'application/json'
            },
            signal: signal,
            timeout: 30000 // 30 second timeout
        });

        // Check if modal is still open
        if (!aiDescriptionModalState.isOpen) {
            console.log('Modal closed, ignoring response');
            return;
        }

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();

        if (data.success) {
            aiDescriptionModalState.setLoading(false);
            aiDescriptionModalState.displayGenerated(data.description);
        } else {
            Swal.fire({
                icon: 'error',
                title: 'Generation Failed',
                text: data.message || 'Failed to generate description.'
            });
            aiDescriptionModalState.setLoading(false);
        }
    } catch (error) {
        // Only show error if modal is still open
        if (!aiDescriptionModalState.isOpen) {
            console.log('Modal closed, error suppressed');
            return;
        }

        if (error.name === 'AbortError') {
            console.log('Generation request was aborted by user');
            // Don't show error for intentional abort
            return;
        }

        console.error('Error:', error);
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'An error occurred while generating the description. Please try again.'
        });
        aiDescriptionModalState.setLoading(false);
    }
}

/**
 * Modal button event handlers
 */

// Cancel button - closes modal with confirmation if generating
$(document).on('click', '#ai-modal-cancel-btn', function () {
    aiDescriptionModalState.close(true);
});

// Close button (X) - closes modal with confirmation if generating
$(document).on('click', '#ai-modal-close-btn', function () {
    aiDescriptionModalState.close(true);
});

// Regenerate button - regenerates description
$(document).on('click', '#ai-modal-regenerate-btn', function () {
    if (!aiDescriptionModalState.isGenerating && aiDescriptionModalState.bookId) {
        generateDescription(aiDescriptionModalState.bookId);
    }
});

// Accept & Replace button - accepts generated description and closes modal
$(document).on('click', '#ai-modal-accept-btn', function () {
    aiDescriptionModalState.acceptAndReplace();
});

/**
 * Prevent closing modal by clicking outside (on backdrop)
 * Modal can only be closed via the buttons
 */
$(document).on('click', '#ai-description-modal-backdrop', function (e) {
    if (e.target === this) {
        // Prevent default close, require using the close button
        // User must explicitly interact with close/cancel button
        if (aiDescriptionModalState.isGenerating) {
            Swal.fire({
                icon: 'info',
                title: 'Generation in Progress',
                text: 'Please use the Cancel button to stop the generation.',
                timer: 3000
            });
        }
    }
});
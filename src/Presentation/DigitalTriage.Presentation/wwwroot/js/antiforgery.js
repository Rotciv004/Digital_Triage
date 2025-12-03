// Antiforgery Token Helper for Blazor Server
// This file provides JavaScript functions to retrieve and use antiforgery tokens

/**
 * Gets the antiforgery token from the page
 * @returns {string|null} The antiforgery token or null if not found
 */
window.getAntiforgeryToken = function() {
    // Method 1: Try to get from hidden input field
    const hiddenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    if (hiddenInput) {
        return hiddenInput.value;
    }
    
    // Method 2: Try to get from cookie
    const cookies = document.cookie.split(';');
    for (let i = 0; i < cookies.length; i++) {
        const cookie = cookies[i].trim();
        if (cookie.startsWith('__RequestVerificationToken=')) {
            return decodeURIComponent(cookie.substring('__RequestVerificationToken='.length));
        }
    }
    
    return null;
};

/**
 * Fetches the antiforgery token from the server endpoint
 * @returns {Promise<string|null>} The antiforgery token or null if error
 */
window.fetchAntiforgeryToken = async function() {
    try {
        const response = await fetch('/antiforgery/token', {
            method: 'GET',
            credentials: 'include',
            headers: {
                'Accept': 'application/json'
            }
        });
        
        if (response.ok) {
            const data = await response.json();
            return data.token;
        } else {
            console.error('Failed to fetch antiforgery token:', response.status);
        }
    } catch (error) {
        console.error('Error fetching antiforgery token:', error);
    }
    return null;
};

/**
 * Makes a POST request with antiforgery token in header
 * @param {string} url - The URL to POST to
 * @param {object} data - The data to send
 * @returns {Promise<Response>} The fetch response
 */
window.postWithAntiforgery = async function(url, data) {
    const token = await window.fetchAntiforgeryToken() || window.getAntiforgeryToken();
    
    if (!token) {
        throw new Error('Antiforgery token not found');
    }
    
    return fetch(url, {
        method: 'POST',
        credentials: 'include',
        headers: {
            'Content-Type': 'application/json',
            'X-CSRF-TOKEN': token
        },
        body: JSON.stringify(data)
    });
};

/**
 * Gets selected option values from a multi-select element
 * @param {HTMLElement} selectElement - The select element reference
 * @returns {string[]} Array of selected option values
 */
window.getSelectedOptions = function(selectElement) {
    if (!selectElement || !selectElement.options) {
        return [];
    }
    
    const selectedValues = [];
    for (let i = 0; i < selectElement.options.length; i++) {
        if (selectElement.options[i].selected) {
            selectedValues.push(selectElement.options[i].value);
        }
    }
    
    return selectedValues;
};

// Export for use in Blazor
window.BlazorAntiforgery = {
    getToken: window.getAntiforgeryToken,
    fetchToken: window.fetchAntiforgeryToken,
    postWithAntiforgery: window.postWithAntiforgery
};


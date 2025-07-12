/**
 * Animated Music Plugin - Theme JavaScript
 * 
 * This script automatically loads the animated cover component for the Jellyfin web UI.
 * 
 * How to use:
 * 1. Copy this entire file content
 * 2. Go to your Jellyfin Dashboard → Display → Custom CSS/JS
 * 3. Paste this code into the "Custom JavaScript" section
 * 4. Save and refresh your Jellyfin web UI
 * 
 * Alternative methods:
 * - Use a browser extension like Tampermonkey
 * - Use a Jellyfin theme manager plugin
 * - Add to your custom theme files
 */

(function() {
    'use strict';
    
    // Only inject once per page
    if (window.__animatedMusicInjected) {
        console.log('Animated Music Plugin: Already loaded');
        return;
    }
    window.__animatedMusicInjected = true;
    
    console.log('Animated Music Plugin: Loading animated cover component...');

    // Function to load the animated cover component script
    function loadAnimatedMusicScript() {
        // Check if script is already loaded
        if (document.getElementById('animated-music-cover-script')) {
            console.log('Animated Music Plugin: Script already loaded');
            return;
        }
        
        // Create script element
        var script = document.createElement('script');
        script.id = 'animated-music-cover-script';
        script.src = '/Plugins/AnimatedMusic/Web/AnimatedCoverComponent.js';
        script.type = 'text/javascript';
        script.async = false;
        
        // Add error handling
        script.onerror = function() {
            console.error('Animated Music Plugin: Failed to load animated cover component');
        };
        
        script.onload = function() {
            console.log('Animated Music Plugin: Animated cover component loaded successfully');
        };
        
        // Append to document
        document.head.appendChild(script);
    }

    // Function to wait for Jellyfin to be ready
    function waitForJellyfin() {
        // Check if Jellyfin is loaded
        if (typeof window.ApiClient !== 'undefined' && window.ApiClient.isInitialized()) {
            loadAnimatedMusicScript();
        } else {
            // Wait a bit and try again
            setTimeout(waitForJellyfin, 1000);
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            setTimeout(waitForJellyfin, 500);
        });
    } else {
        setTimeout(waitForJellyfin, 500);
    }
    
    // Also try to load when navigating to new pages (for SPA behavior)
    let currentUrl = location.href;
    const observer = new MutationObserver(function() {
        const url = location.href;
        if (url !== currentUrl) {
            currentUrl = url;
            setTimeout(loadAnimatedMusicScript, 1000);
        }
    });
    
    observer.observe(document, { subtree: true, childList: true });
    
    console.log('Animated Music Plugin: Theme script initialized');
})(); 
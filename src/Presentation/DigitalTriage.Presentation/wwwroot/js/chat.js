// ===========================
// Digital Triage - Chat Specific Functions
// ===========================

// Auto-scroll chat to bottom (duplicate for redundancy)
window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// Smooth scroll animation for chat
window.smoothScrollToBottom = (element) => {
    if (element) {
        element.scrollTo({
      top: element.scrollHeight,
        behavior: 'smooth'
        });
    }
};

console.log('? chat.js loaded successfully');
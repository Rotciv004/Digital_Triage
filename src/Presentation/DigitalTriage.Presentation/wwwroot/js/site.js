// ===========================
// Digital Triage - Main JavaScript Functions
// ===========================

// Scroll to bottom function for chat
window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// Get selected options from multi-select
window.getSelectedOptions = (selectElement) => {
 if (!selectElement) return [];
    
const options = selectElement.options;
    const selected = [];
    
    for (let i = 0; i < options.length; i++) {
 if (options[i].selected) {
   selected.push(options[i].value);
   }
    }
    
    return selected;
};

// Auto-dismiss alerts after 5 seconds
document.addEventListener('DOMContentLoaded', function() {
    setTimeout(function() {
        const alerts = document.querySelectorAll('.alert.alert-dismissible');
        alerts.forEach(function(alert) {
       const bsAlert = new bootstrap.Alert(alert);
  bsAlert.close();
   });
    }, 5000);
});

console.log('? site.js loaded successfully');

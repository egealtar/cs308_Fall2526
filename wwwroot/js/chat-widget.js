// Chat Widget JavaScript
document.addEventListener('DOMContentLoaded', function () {
    const chatWidgetToggle = document.getElementById('chatWidgetToggle');
    const chatWidgetPanel = document.getElementById('chatWidgetPanel');
    const chatWidgetClose = document.getElementById('chatWidgetClose');
    const chatNotificationBadge = document.getElementById('chatNotificationBadge');

    if (chatWidgetToggle && chatWidgetPanel) {
        chatWidgetToggle.addEventListener('click', function () {
            chatWidgetPanel.classList.toggle('d-none');
        });
    }

    if (chatWidgetClose && chatWidgetPanel) {
        chatWidgetClose.addEventListener('click', function () {
            chatWidgetPanel.classList.add('d-none');
        });
    }

    // Close panel when clicking outside
    document.addEventListener('click', function (e) {
        if (chatWidgetPanel && !chatWidgetPanel.contains(e.target) && 
            chatWidgetToggle && !chatWidgetToggle.contains(e.target) &&
            !chatWidgetPanel.classList.contains('d-none')) {
            chatWidgetPanel.classList.add('d-none');
        }
    });

    // Check for unread messages
    function checkUnreadMessages() {
        fetch('/Chat/GetUnreadCount')
            .then(response => response.json())
            .then(data => {
                if (chatNotificationBadge) {
                    if (data.unreadCount > 0) {
                        chatNotificationBadge.textContent = data.unreadCount > 99 ? '99+' : data.unreadCount;
                        chatNotificationBadge.classList.remove('d-none');
                    } else {
                        chatNotificationBadge.classList.add('d-none');
                    }
                }
            })
            .catch(error => {
                console.error('Error checking unread messages:', error);
            });
    }

    // Initialize SignalR connection for real-time notifications
    if (typeof signalR !== 'undefined') {
        const notificationConnection = new signalR.HubConnectionBuilder()
            .withUrl("/chathub")
            .build();

        notificationConnection.start().then(function () {
            console.log("Connected to chat notification hub");
        }).catch(function (err) {
            console.error("Error connecting to notification hub:", err);
        });

        // Listen for new agent messages
        notificationConnection.on("NewAgentMessage", function (chatId) {
            checkUnreadMessages();
        });
    }

    // Check immediately and then every 5 seconds as fallback
    checkUnreadMessages();
    setInterval(checkUnreadMessages, 5000);
});


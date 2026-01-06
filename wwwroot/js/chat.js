// Customer Chat JavaScript
let connection;
let chatId;
let isTyping = false;
let typingTimeout;

document.addEventListener('DOMContentLoaded', function () {
    chatId = document.querySelector('input[name="chatId"]')?.value;
    if (!chatId) return;

    // Initialize SignalR connection
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .build();

    connection.start().then(function () {
        console.log("Connected to chat hub");
        connection.invoke("JoinChat", chatId);
    }).catch(function (err) {
        console.error("Error connecting to chat hub:", err);
    });

    // Receive messages
    connection.on("ReceiveMessage", function (message) {
        addMessageToChat(message);
        
        // If message is from agent, update notification badge
        if (message.senderType === "Agent") {
            updateNotificationBadge();
        }
    });

    // Typing indicator
    connection.on("UserTyping", function (senderName, typing) {
        // Could add typing indicator UI here
    });

    // Chat form submission
    const chatForm = document.getElementById('chatForm');
    const messageInput = document.getElementById('messageInput');
    const fileInput = document.getElementById('fileInput');

    chatForm.addEventListener('submit', async function (e) {
        e.preventDefault();
        
        const content = messageInput.value.trim();
        const file = fileInput.files[0];

        if (!content && !file) {
            return;
        }

        if (file) {
            await uploadFile(file);
        } else if (content) {
            await sendMessage(content);
        }

        messageInput.value = '';
        fileInput.value = '';
        document.getElementById('filePreview').innerHTML = '';
    });

    // File input change
    fileInput.addEventListener('change', function (e) {
        const file = e.target.files[0];
        if (file) {
            const preview = document.getElementById('filePreview');
            preview.innerHTML = `
                <div class="alert alert-info">
                    <i class="bi bi-file-earmark"></i> ${file.name} 
                    (${(file.size / 1024).toFixed(2)} KB)
                    <button type="button" class="btn btn-sm btn-outline-danger ms-2" onclick="clearFile()">
                        <i class="bi bi-x"></i>
                    </button>
                </div>
            `;
        }
    });

    // Typing indicator
    messageInput.addEventListener('input', function () {
        if (!isTyping) {
            isTyping = true;
            connection.invoke("Typing", chatId, "Customer", true);
        }

        clearTimeout(typingTimeout);
        typingTimeout = setTimeout(function () {
            isTyping = false;
            connection.invoke("Typing", chatId, "Customer", false);
        }, 1000);
    });

    // Scroll to bottom on load
    scrollToBottom();
    
    // Mark all agent messages as read when chat is opened
    markMessagesAsRead();
});

function updateNotificationBadge() {
    fetch('/Chat/GetUnreadCount')
        .then(response => response.json())
        .then(data => {
            const badge = document.getElementById('chatNotificationBadge');
            if (badge) {
                if (data.unreadCount > 0) {
                    badge.textContent = data.unreadCount > 99 ? '99+' : data.unreadCount;
                    badge.classList.remove('d-none');
                } else {
                    badge.classList.add('d-none');
                }
            }
        })
        .catch(error => console.error('Error updating notification badge:', error));
}

async function markMessagesAsRead() {
    if (!chatId) return;
    
    const formData = new FormData();
    formData.append('chatId', chatId);
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    if (token) {
        formData.append('__RequestVerificationToken', token.value);
    }
    
    try {
        await fetch('/Chat/MarkMessagesAsRead', {
            method: 'POST',
            body: formData
        });
        updateNotificationBadge();
    } catch (error) {
        console.error('Error marking messages as read:', error);
    }
}

function clearFile() {
    document.getElementById('fileInput').value = '';
    document.getElementById('filePreview').innerHTML = '';
}

async function sendMessage(content) {
    const formData = new FormData();
    formData.append('chatId', chatId);
    formData.append('content', content);
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    if (token) {
        formData.append('__RequestVerificationToken', token.value);
    }

    try {
        const response = await fetch('/Chat/SendMessage', {
            method: 'POST',
            body: formData
        });

        if (response.ok) {
            const result = await response.json();
            // Message will be added via SignalR
        } else {
            alert('Failed to send message');
        }
    } catch (error) {
        console.error('Error sending message:', error);
        alert('Error sending message');
    }
}

async function uploadFile(file) {
    const formData = new FormData();
    formData.append('chatId', chatId);
    formData.append('file', file);
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    if (token) {
        formData.append('__RequestVerificationToken', token.value);
    }

    try {
        const response = await fetch('/Chat/UploadAttachment', {
            method: 'POST',
            body: formData
        });

        if (response.ok) {
            const result = await response.json();
            // Message will be added via SignalR
        } else {
            const error = await response.text();
            alert('Failed to upload file: ' + error);
        }
    } catch (error) {
        console.error('Error uploading file:', error);
        alert('Error uploading file');
    }
}

function addMessageToChat(message) {
    const messagesContainer = document.getElementById('chatMessages');
    const messageDiv = document.createElement('div');
    
    const isCustomer = message.senderType === 'Customer' || message.senderType === 'Guest';
    const bubbleClass = isCustomer ? 'bg-primary text-white' : 
                       message.senderType === 'Agent' ? 'bg-light' : 'bg-secondary text-white';
    const justifyClass = isCustomer ? 'justify-content-end' : '';

    let attachmentsHtml = '';
    if (message.attachments && message.attachments.length > 0) {
        attachmentsHtml = '<div class="message-attachments mt-2">';
        message.attachments.forEach(att => {
            attachmentsHtml += `
                <div class="attachment-item">
                    <a href="${att.filePath}" target="_blank" class="btn btn-sm btn-outline-secondary">
                        <i class="bi bi-paperclip"></i> ${att.fileName}
                        <small>(${(att.fileSize / 1024).toFixed(2)} KB)</small>
                    </a>
                </div>
            `;
        });
        attachmentsHtml += '</div>';
    }

    const date = new Date(message.createdAt);
    messageDiv.className = `message mb-3 ${isCustomer ? 'message-customer' : message.senderType === 'Agent' ? 'message-agent' : 'message-system'}`;
    messageDiv.innerHTML = `
        <div class="d-flex ${justifyClass}">
            <div class="message-bubble ${bubbleClass}" style="max-width: 70%; padding: 10px; border-radius: 10px;">
                <div class="message-header" style="font-size: 0.85em; opacity: 0.8; margin-bottom: 5px;">
                    ${message.senderName}
                    ${message.senderType === 'Agent' ? '<span class="badge bg-info">Agent</span>' : ''}
                </div>
                <div class="message-content">${escapeHtml(message.content)}</div>
                ${attachmentsHtml}
                <div class="message-time" style="font-size: 0.75em; opacity: 0.7; margin-top: 5px;">
                    ${date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })}
                </div>
            </div>
        </div>
    `;

    messagesContainer.appendChild(messageDiv);
    scrollToBottom();
}

function scrollToBottom() {
    const messagesContainer = document.getElementById('chatMessages');
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}


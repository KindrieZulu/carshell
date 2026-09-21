// Backs the Account Settings page: load the signed-in admin's own profile,
// save display/contact details, and upload an avatar via the same
// signed-URL pattern the listing image uploader uses.
function setError(elementId, message) {
    const el = document.getElementById(elementId);
    el.textContent = message;
    el.hidden = !message;
}

function renderAvatar(storageKey, fallbackLetter) {
    const preview = document.getElementById('avatar-preview');
    if (storageKey) {
        preview.innerHTML = `<img src="${window.CARSHELL_CONFIG.storageBaseUrl}/${storageKey}" alt="" />`;
    } else {
        preview.textContent = (fallbackLetter || '?').toUpperCase();
    }
}

async function loadSettings() {
    const response = await CarShellAdmin.apiFetch('/api/settings/me');
    if (!response.ok) {
        setError('form-error', 'Could not load your settings.');
        return;
    }
    const settings = await response.json();

    document.getElementById('login-email').value = settings.email;
    document.getElementById('username').value = settings.username || '';
    document.getElementById('contact-email').value = settings.contactEmail || '';
    document.getElementById('contact-phone').value = settings.contactPhone || '';
    renderAvatar(settings.profilePictureStorageKey, settings.username || settings.email);
}

document.getElementById('settings-form').addEventListener('submit', async (event) => {
    event.preventDefault();
    setError('form-error', '');
    document.getElementById('form-success').hidden = true;

    const response = await CarShellAdmin.apiFetch('/api/settings/me', {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            username: document.getElementById('username').value || null,
            contactEmail: document.getElementById('contact-email').value || null,
            contactPhone: document.getElementById('contact-phone').value || null,
        }),
    });

    if (!response.ok) {
        const text = await response.text();
        setError('form-error', text || `Save failed (HTTP ${response.status}).`);
        return;
    }

    const saved = await response.json();
    renderAvatar(saved.profilePictureStorageKey, saved.username || saved.email);
    document.getElementById('form-success').hidden = false;
    document.getElementById('form-success').textContent = 'Saved.';
});

document.getElementById('avatar-upload-btn').addEventListener('click', async () => {
    setError('avatar-error', '');
    const fileInput = document.getElementById('avatar-file');
    const file = fileInput.files[0];
    if (!file) {
        setError('avatar-error', 'Choose a photo first.');
        return;
    }

    try {
        const signResponse = await CarShellAdmin.apiFetch('/api/settings/me/avatar/upload-url', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ contentType: file.type }),
        });
        if (!signResponse.ok) {
            throw new Error(await signResponse.text() || 'Could not get an upload URL.');
        }
        const { uploadUrl, storageKey } = await signResponse.json();

        const putResponse = await fetch(uploadUrl, {
            method: 'PUT',
            headers: { 'Content-Type': file.type },
            body: file,
        });
        if (!putResponse.ok) {
            throw new Error('Upload to storage failed.');
        }

        const confirmResponse = await CarShellAdmin.apiFetch('/api/settings/me/avatar/confirm', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ storageKey }),
        });
        if (!confirmResponse.ok) {
            throw new Error(await confirmResponse.text() || 'Could not confirm the upload.');
        }
        const confirmed = await confirmResponse.json();

        fileInput.value = '';
        renderAvatar(confirmed.profilePictureStorageKey, null);
        if (window.CarShellTopbarAvatar) {
            window.CarShellTopbarAvatar.refresh();
        }
    } catch (err) {
        setError('avatar-error', err.message);
    }
});

loadSettings().catch((err) => setError('form-error', err.message));

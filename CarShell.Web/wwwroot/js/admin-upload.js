// Backs the Vehicle Upload & Media Suite page: a 4-step guided flow over
// the same real listing fields and endpoints the old single-page form
// used. Hidden step panels are skipped by native form validation (the
// HTML spec bars non-rendered elements from constraint validation), so
// required fields in a step the admin has not reached yet do not block
// submission of a later step.
const params = new URLSearchParams(window.location.search);
const listingId = params.get('id');
const isEditMode = Boolean(listingId);
let currentVersion = 0;

const totalSteps = 4;
let currentStep = 1;

function updateStepUI() {
    document.querySelectorAll('.step-panel').forEach((panel) => {
        panel.hidden = Number(panel.dataset.stepPanel) !== currentStep;
    });
    document.querySelectorAll('.stepper-step').forEach((step) => {
        const stepNum = Number(step.dataset.step);
        step.classList.toggle('active', stepNum === currentStep);
        step.classList.toggle('done', stepNum < currentStep);
    });
    document.getElementById('step-back-btn').hidden = currentStep === 1;
    document.getElementById('step-next-btn').hidden = currentStep === totalSteps;
}

function goToStep(step) {
    currentStep = Math.min(Math.max(step, 1), totalSteps);
    updateStepUI();
}

document.querySelectorAll('.stepper-step').forEach((step) => {
    step.addEventListener('click', () => goToStep(Number(step.dataset.step)));
});
document.getElementById('step-back-btn').addEventListener('click', () => goToStep(currentStep - 1));
document.getElementById('step-next-btn').addEventListener('click', () => goToStep(currentStep + 1));
updateStepUI();

function setError(elementId, message) {
    const el = document.getElementById(elementId);
    el.textContent = message;
    el.hidden = !message;
}

async function populateMakes(selectedMakeId) {
    const response = await fetch('/api/makes');
    const makes = await response.json();
    const select = document.getElementById('make-id');
    select.innerHTML = '';
    for (const make of makes) {
        const option = document.createElement('option');
        option.value = make.id;
        option.textContent = make.name;
        select.appendChild(option);
    }
    if (selectedMakeId) {
        select.value = selectedMakeId;
    }
}

async function populateModels(makeId, selectedModelId) {
    const response = await fetch(`/api/makes/${makeId}/models`);
    const models = await response.json();
    const select = document.getElementById('model-id');
    select.innerHTML = '';
    for (const model of models) {
        const option = document.createElement('option');
        option.value = model.id;
        option.textContent = model.name;
        select.appendChild(option);
    }
    if (selectedModelId) {
        select.value = selectedModelId;
    }
}

async function populateSuburbs(selectedSuburbId) {
    const response = await fetch('/api/suburbs');
    const suburbs = await response.json();
    const select = document.getElementById('suburb-id');
    select.innerHTML = '';
    for (const suburb of suburbs) {
        const option = document.createElement('option');
        option.textContent = `${suburb.city} — ${suburb.name}`;
        option.value = suburb.id;
        select.appendChild(option);
    }
    if (selectedSuburbId) {
        select.value = selectedSuburbId;
    }
}

document.getElementById('make-id').addEventListener('change', (event) => {
    populateModels(event.target.value);
});

document.getElementById('status').addEventListener('change', (event) => {
    document.getElementById('sale-price-label').hidden = event.target.value !== 'Sold';
});

async function loadImages() {
    const response = await CarShellAdmin.apiFetch(`/api/listings/${listingId}`);
    const listing = await response.json();
    const list = document.getElementById('image-list');
    list.innerHTML = '';
    for (const image of listing.images) {
        const item = document.createElement('li');
        const img = document.createElement('img');
        img.src = `${window.CARSHELL_CONFIG.storageBaseUrl}/${image.storageKey}`;
        img.alt = '';
        item.appendChild(img);
        list.appendChild(item);
    }
}

async function loadExistingListing() {
    document.getElementById('page-heading').textContent = 'Edit Vehicle Listing';
    document.getElementById('status-section').hidden = false;
    document.getElementById('delete-btn').hidden = false;
    document.getElementById('submit-btn').textContent = 'Save Changes';
    document.getElementById('media-locked-message').hidden = true;
    document.getElementById('images-section').hidden = false;

    const response = await CarShellAdmin.apiFetch(`/api/listings/${listingId}`);
    if (!response.ok) {
        setError('load-error', 'Could not load this listing.');
        return;
    }
    const listing = await response.json();

    await populateMakes(listing.makeId);
    await populateModels(listing.makeId, listing.modelId);
    await populateSuburbs(listing.suburbId);

    document.getElementById('vin').value = listing.vin;
    document.getElementById('trim').value = listing.trim || '';
    document.getElementById('year').value = listing.year;
    document.getElementById('mileage').value = listing.mileage;
    document.getElementById('engine-capacity').value = listing.engineCapacityLitres;
    document.getElementById('price').value = listing.price;
    document.getElementById('fuel-type').value = listing.fuelType;
    document.getElementById('transmission').value = listing.transmission;
    document.getElementById('body-type').value = listing.bodyType;
    document.getElementById('description').value = listing.description || '';
    document.getElementById('status').value = listing.status;
    document.getElementById('sale-price-label').hidden = listing.status !== 'Sold';
    currentVersion = listing.version;

    await loadImages();
}

function readForm() {
    return {
        makeId: Number(document.getElementById('make-id').value),
        modelId: Number(document.getElementById('model-id').value),
        trim: document.getElementById('trim').value || null,
        year: Number(document.getElementById('year').value),
        mileage: Number(document.getElementById('mileage').value),
        engineCapacityLitres: Number(document.getElementById('engine-capacity').value),
        price: Number(document.getElementById('price').value),
        fuelType: document.getElementById('fuel-type').value,
        transmission: document.getElementById('transmission').value,
        bodyType: document.getElementById('body-type').value,
        description: document.getElementById('description').value || null,
        vin: document.getElementById('vin').value,
        suburbId: Number(document.getElementById('suburb-id').value),
    };
}

document.getElementById('listing-form').addEventListener('submit', async (event) => {
    event.preventDefault();
    setError('form-error', '');
    document.getElementById('form-success').hidden = true;

    const body = readForm();

    if (isEditMode) {
        body.version = currentVersion;
        const statusValue = document.getElementById('status').value;
        body.status = statusValue;
        body.salePrice = statusValue === 'Sold'
            ? Number(document.getElementById('sale-price').value)
            : null;
    }

    const response = await CarShellAdmin.apiFetch(
        isEditMode ? `/api/listings/${listingId}` : '/api/listings',
        {
            method: isEditMode ? 'PATCH' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });

    if (!response.ok) {
        const text = await response.text();
        setError('form-error', text || `Save failed (HTTP ${response.status}).`);
        return;
    }

    if (isEditMode) {
        const saved = await response.json();
        currentVersion = saved.version;
        document.getElementById('form-success').hidden = false;
        document.getElementById('form-success').textContent = 'Saved.';
    } else {
        const created = await response.json();
        window.location.href = `/admin/listings/edit?id=${created.id}`;
    }
});

document.getElementById('delete-btn').addEventListener('click', async () => {
    if (!confirm('Delete this listing? It will be removed from public search.')) {
        return;
    }
    const response = await CarShellAdmin.apiFetch(`/api/listings/${listingId}`, { method: 'DELETE' });
    if (response.ok) {
        window.location.href = '/admin/listings';
    } else {
        setError('form-error', `Delete failed (HTTP ${response.status}).`);
    }
});

document.getElementById('upload-btn').addEventListener('click', async () => {
    setError('upload-error', '');
    const fileInput = document.getElementById('image-file');
    const file = fileInput.files[0];
    if (!file) {
        setError('upload-error', 'Choose a file first.');
        return;
    }

    try {
        const signResponse = await CarShellAdmin.apiFetch(`/api/listings/${listingId}/images/upload-url`, {
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

        const confirmResponse = await CarShellAdmin.apiFetch(`/api/listings/${listingId}/images/confirm`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ storageKey }),
        });
        if (!confirmResponse.ok) {
            throw new Error(await confirmResponse.text() || 'Could not confirm the upload.');
        }

        fileInput.value = '';
        await loadImages();
    } catch (err) {
        setError('upload-error', err.message);
    }
});

(async () => {
    if (isEditMode) {
        await loadExistingListing();
    } else {
        document.getElementById('media-locked-message').hidden = false;
        await populateMakes();
        await populateModels(document.getElementById('make-id').value);
        await populateSuburbs();
    }
})().catch((err) => setError('load-error', err.message));

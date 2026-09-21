// Backs the Admin Dashboard page: KPI row, governance panel, vehicle
// ingestion pipeline cards, and the body-type distribution bar. All of it
// is derived from real data via CarShellAdmin.apiFetch — nothing here is
// fabricated or hard-coded.
const statusBadgeClass = { Draft: 'badge-draft', Active: 'badge-active', Sold: 'badge-sold', Removed: 'badge-removed' };
const bodyTypeColors = ['#00D9E4', '#7C5CFC', '#FF6B35', '#17E6A1', '#FFB84D', '#FF4D8D', '#4DA3FF', '#B98CFF'];

function carIconSvg() {
    return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><path stroke-linecap="round" stroke-linejoin="round" d="M3 13l1.5-5A2 2 0 016.4 6.5h11.2A2 2 0 0119.5 8l1.5 5M5 13h14a1 1 0 011 1v4a1 1 0 01-1 1h-1a1 1 0 01-1-1v-1H7v1a1 1 0 01-1 1H5a1 1 0 01-1-1v-4a1 1 0 011-1z"/><circle cx="7.5" cy="16.5" r="1"/><circle cx="16.5" cy="16.5" r="1"/></svg>';
}

async function loadGovernance() {
    const card = document.getElementById('governance-card');
    const response = await CarShellAdmin.apiFetch('/api/admin/admins');

    if (response.status === 403) {
        card.hidden = false;
        document.getElementById('governance-table').hidden = true;
        document.getElementById('governance-restricted').hidden = false;
        return;
    }
    if (!response.ok) {
        return;
    }

    const admins = await response.json();
    card.hidden = false;

    const tbody = document.getElementById('governance-body');
    tbody.innerHTML = '';
    for (const admin of admins) {
        const initials = (admin.username || admin.email || '?').charAt(0).toUpperCase();
        const accessClass = admin.isSuperAdmin ? 'badge-full-access' : 'badge-standard-access';
        const accessLabel = admin.isSuperAdmin ? 'Full Access' : 'Standard Access';
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>
                <div class="person-cell">
                    <div class="table-avatar">${initials}</div>
                    <div>
                        <div class="vehicle-title">${admin.username || admin.email}</div>
                        <div class="vehicle-sub">${admin.email}</div>
                    </div>
                </div>
            </td>
            <td><span class="badge badge-dot ${accessClass}">${accessLabel}</span></td>
            <td>${new Date(admin.createdAt).toLocaleDateString()}</td>
        `;
        tbody.appendChild(row);
    }
}

function renderSegBar(listings) {
    const counts = {};
    for (const listing of listings) {
        if (listing.status === 'Removed') continue;
        counts[listing.bodyType] = (counts[listing.bodyType] || 0) + 1;
    }
    const entries = Object.entries(counts).sort((a, b) => b[1] - a[1]);
    const total = entries.reduce((sum, [, count]) => sum + count, 0);

    const bar = document.getElementById('segbar');
    const legend = document.getElementById('segbar-legend');
    bar.innerHTML = '';
    legend.innerHTML = '';

    if (total === 0) {
        legend.innerHTML = '<span style="color:var(--color-ink-muted); font-size:0.85rem;">No active inventory yet.</span>';
        return;
    }

    entries.forEach(([bodyType, count], index) => {
        const color = bodyTypeColors[index % bodyTypeColors.length];
        const pct = (count / total) * 100;

        const span = document.createElement('span');
        span.style.width = pct + '%';
        span.style.background = color;
        bar.appendChild(span);

        const item = document.createElement('div');
        item.className = 'segbar-legend-item';
        item.innerHTML = `<span class="segbar-legend-dot" style="background:${color}"></span> ${bodyType} &middot; ${count} (${pct.toFixed(0)}%)`;
        legend.appendChild(item);
    });
}

async function loadListings() {
    const response = await CarShellAdmin.apiFetch('/api/listings/mine');
    const listings = await response.json();

    document.getElementById('loading-message').hidden = true;

    const total = listings.length;
    const active = listings.filter((l) => l.status === 'Active');
    const sold = listings.filter((l) => l.status === 'Sold').length;
    const draft = listings.filter((l) => l.status === 'Draft').length;
    const activeValue = active.reduce((sum, l) => sum + l.price, 0);

    document.getElementById('stat-value').textContent = '$' + activeValue.toLocaleString();
    document.getElementById('stat-total').textContent = total.toLocaleString();
    document.getElementById('stat-sold').textContent = sold.toLocaleString();
    document.getElementById('stat-draft').textContent = draft.toLocaleString();
    document.getElementById('stat-grid').hidden = false;

    renderSegBar(listings);

    const row = document.getElementById('pipeline-row');
    row.innerHTML = '';

    if (total === 0) {
        document.getElementById('pipeline-empty').hidden = false;
        return;
    }
    document.getElementById('pipeline-empty').hidden = true;

    for (const listing of listings) {
        const card = document.createElement('a');
        card.className = 'pipeline-card';
        card.href = `/admin/listings/edit?id=${listing.id}`;
        const thumb = listing.coverImageStorageKey
            ? `<img src="${window.CARSHELL_CONFIG.storageBaseUrl}/${listing.coverImageStorageKey}" alt="" />`
            : carIconSvg();
        card.innerHTML = `
            <div class="pipeline-thumb">${thumb}</div>
            <div class="pipeline-title">${listing.year} ${listing.make} ${listing.model}</div>
            <div class="pipeline-price">$${listing.price.toLocaleString()}</div>
            <div class="pipeline-meta">
                <span class="badge badge-dot ${statusBadgeClass[listing.status] || ''}">${listing.status}</span>
                <span>${listing.mileage.toLocaleString()} km</span>
            </div>
            <div class="pipeline-meta"><span>${listing.suburb}, ${listing.city}</span></div>
        `;
        row.appendChild(card);
    }
}

document.getElementById('refresh-dock-btn').addEventListener('click', () => {
    const loadingEl = document.getElementById('loading-message');
    loadingEl.hidden = false;
    loadingEl.textContent = 'Refreshing…';
    loadListings()
        .then(() => { loadingEl.hidden = true; })
        .catch((err) => { loadingEl.textContent = 'Could not refresh: ' + err.message; });
});

loadListings().catch((err) => {
    document.getElementById('loading-message').textContent = 'Could not load listings: ' + err.message;
});
loadGovernance().catch(() => {
    document.getElementById('governance-card').hidden = true;
});

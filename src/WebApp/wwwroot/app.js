let map;
let markers = [];
let markerMap = new Map();   // listingId → marker
let activeMarkerId = null;
let tooltipsVisible = true;
let currentListings = [];
let currentFavorites = [];
let favoriteIds = new Set();
let activeView = 'favorites';
let currentListingId = null;

// ── Map init ─────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  map = L.map('map', { zoomControl: true }).setView([25.033, 121.565], 13);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    maxZoom: 19
  }).addTo(map);

  loadView('favorites');
});

// ── View switching ───────────────────────────────────────────────
window.switchView = function (view) {
  activeView = view;
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.view === view));
  loadView(view);
};

async function loadView(view) {
  clearMarkers();
  if (view === 'favorites') {
    await loadFavorites();
  } else {
    await loadAllListings();
  }
}

// ── Favorites view ───────────────────────────────────────────────
async function loadFavorites() {
  const favs = await apiFetch('/api/favorites');
  currentFavorites = favs;
  favoriteIds = new Set(favs.map(f => f.listing_id));

  const listings = favs.map(f => f.listing).filter(Boolean);
  document.getElementById('countBadge').textContent = `收藏 ${listings.length} 筆`;
  document.getElementById('listTitle').textContent = '收藏物件';
  renderList(listings, favs);
  renderMarkers(listings);
}

// ── All listings view ────────────────────────────────────────────
async function loadAllListings() {
  const listings = await apiFetch('/api/listings');
  currentListings = listings;
  document.getElementById('countBadge').textContent = `${listings.length} 筆物件`;
  document.getElementById('listTitle').textContent = '所有物件';
  renderList(listings, currentFavorites);
  renderMarkers(listings);
}

// ── Filter ───────────────────────────────────────────────────────
window.applyFilters = async function () {
  const hasFurniture = document.getElementById('filterFurniture').checked;
  const hasInternet  = document.getElementById('filterInternet').checked;
  const hasGas       = document.getElementById('filterGas').checked;
  const hasParking   = document.getElementById('filterParking').checked;
  const hasPet       = document.getElementById('filterPet').checked;
  const maxPrice     = parseFloat(document.getElementById('filterMaxPrice').value) || null;
  const minPing      = parseFloat(document.getElementById('filterMinPing').value)  || null;
  const maxPing      = parseFloat(document.getElementById('filterMaxPing').value)  || null;

  function matchesFilters(l) {
    if (hasFurniture && !l.has_furniture)   return false;
    if (hasInternet  && !l.has_internet)    return false;
    if (hasGas       && !l.has_natural_gas) return false;
    if (hasParking   && !l.has_parking)     return false;
    if (hasPet       && !l.pet_allowed)     return false;
    if (maxPrice !== null && l.price > maxPrice)       return false;
    if (minPing  !== null && l.size_ping < minPing)    return false;
    if (maxPing  !== null && l.size_ping > maxPing)    return false;
    return true;
  }

  clearMarkers();

  if (activeView === 'favorites') {
    const listings = currentFavorites.map(f => f.listing).filter(Boolean).filter(matchesFilters);
    renderList(listings, currentFavorites);
    renderMarkers(listings);
    return;
  }

  const params = new URLSearchParams();
  if (hasFurniture) params.set('hasFurniture', 'true');
  if (hasInternet)  params.set('hasInternet', 'true');
  if (hasGas)       params.set('hasNaturalGas', 'true');
  if (hasParking)   params.set('hasParking', 'true');
  if (hasPet)       params.set('petAllowed', 'true');
  if (maxPrice)     params.set('maxPrice', maxPrice);
  if (minPing)      params.set('minSizePing', minPing);
  if (maxPing)      params.set('maxSizePing', maxPing);

  const listings = await apiFetch('/api/listings?' + params.toString());
  currentListings = listings;
  renderList(listings, currentFavorites);
  renderMarkers(listings);
};

// ── Render markers ───────────────────────────────────────────────
function makeCircleIcon(color, highlighted = false) {
  const size = highlighted ? 26 : 18;
  const border = highlighted ? '3px solid #fbbf24' : '2px solid #fff';
  const shadow = highlighted
    ? '0 0 0 3px rgba(251,191,36,0.4), 0 2px 6px rgba(0,0,0,0.6)'
    : '0 1px 4px rgba(0,0,0,0.5)';
  return L.divIcon({
    className: '',
    html: `<div style="width:${size}px;height:${size}px;border-radius:50%;
      background:${color};border:${border};box-shadow:${shadow}"></div>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2]
  });
}

function renderMarkers(listings) {
  listings.forEach(listing => {
    if (!listing.lat || !listing.lng) return;

    const isFav = favoriteIds.has(listing.id);
    const marker = L.marker([listing.lat, listing.lng], {
      icon: makeCircleIcon(isFav ? '#10b981' : '#6366f1'),
      title: listing.title
    }).addTo(map);

    marker.bindTooltip(
      `<b>${listing.title}</b><br>$${listing.price.toLocaleString()} / 月`,
      { direction: 'top', offset: [0, -12], permanent: true, opacity: tooltipsVisible ? 0.9 : 0 }
    );
    const fav = currentFavorites.find(f => f.listing_id === listing.id);
    marker.on('click', () => openModal(listing, fav));
    markers.push(marker);
    markerMap.set(listing.id, { marker, listing });
  });
}

function clearMarkers() {
  markers.forEach(m => m.remove());
  markers = [];
  markerMap.clear();
  activeMarkerId = null;
}

// ── Render sidebar list ──────────────────────────────────────────
function renderList(listings, favs) {
  const favMap = Object.fromEntries(favs.map(f => [f.listing_id, f]));
  const container = document.getElementById('listContainer');
  container.innerHTML = '';

  if (listings.length === 0) {
    container.innerHTML = '<p style="color:#64748b;font-size:0.85em;text-align:center;padding:16px">無物件</p>';
    return;
  }

  listings.forEach(listing => {
    const fav = favMap[listing.id];
    const card = document.createElement('div');
    card.className = 'listing-card' + (fav ? ' active' : '');
    card.innerHTML = `
      <div class="card-title" title="${listing.title}">${listing.title}</div>
      <div class="card-price">$${listing.price.toLocaleString()} / 月</div>
      <div class="card-status">${fav ? fav.status : listing.room_type} · ${listing.size_ping} 坪</div>
    `;
    card.addEventListener('click', () => {
      highlightMarker(listing);
      if (listing.lat && listing.lng)
        map.flyTo([listing.lat, listing.lng], 16, { duration: 0.8 });
    });
    container.appendChild(card);
  });
}

// ── Info modal ───────────────────────────────────────────────────
function openModal(listing, fav) {
  currentListingId = listing.id;
  const isFav = favoriteIds.has(listing.id);

  const photo = listing.images?.[0];
  const photoEl = document.getElementById('modalPhoto');
  if (photo) { photoEl.src = photo; photoEl.classList.remove('hidden'); }
  else { photoEl.classList.add('hidden'); }

  document.getElementById('modalTitle').textContent = listing.title;
  document.getElementById('modalPrice').textContent = `$${listing.price.toLocaleString()} / 月`;
  document.getElementById('modalAddress').textContent = listing.address;
  document.getElementById('modalMeta').textContent = `${listing.size_ping} 坪 · ${listing.room_type}`;
  document.getElementById('modalLink').href = listing.url;

  const amenityList = [
    { icon: '🛏', label: '床',    val: listing.has_bed },
    { icon: '👗', label: '衣櫃',  val: listing.has_wardrobe },
    { icon: '🧊', label: '冰箱',  val: listing.has_fridge },
    { icon: '🫧', label: '洗衣機', val: listing.has_washing_machine },
    { icon: '🚿', label: '熱水器', val: listing.has_water_heater },
    { icon: '❄️', label: '冷氣',  val: listing.has_air_con },
    { icon: '📺', label: '電視',  val: listing.has_tv },
    { icon: '🔥', label: '天然氣', val: listing.has_natural_gas },
    { icon: '🌐', label: '網路',  val: listing.has_internet },
    { icon: '📡', label: '第四台', val: listing.has_cable_tv },
    { icon: '🛗', label: '電梯',  val: listing.has_elevator },
    { icon: '🌿', label: '陽台',  val: listing.has_balcony },
    { icon: '🚗', label: '停車',  val: listing.has_parking },
    { icon: '🐾', label: '寵物',  val: listing.pet_allowed },
  ];
  const tags = amenityList
    .filter(a => a.val)
    .map(a => `<span class="amenity-tag">${a.icon} ${a.label}</span>`)
    .join('');
  document.getElementById('modalAmenities').innerHTML =
    tags || '<span style="color:#64748b;font-size:0.8em">無設備資料</span>';

  const statusRow = document.getElementById('modalStatusRow');
  const noteRow = document.getElementById('modalNoteRow');
  if (isFav && fav) {
    statusRow.innerHTML = `<select id="modalStatus" onchange="saveStatus(this.value)">
      ${['待看','已看','已洽談','不考慮'].map(s =>
        `<option value="${s}" ${fav.status === s ? 'selected' : ''}>${s}</option>`
      ).join('')}
    </select>`;
    noteRow.innerHTML = `<textarea id="modalNote" placeholder="備註..." onblur="saveNote(this.value)">${fav.note || ''}</textarea>`;
  } else {
    statusRow.innerHTML = '';
    noteRow.innerHTML = '';
  }

  document.getElementById('modalActions').innerHTML = isFav
    ? `<button class="btn-favorite btn-remove-fav" onclick="removeFavorite()">移除收藏</button>`
    : `<button class="btn-favorite btn-add-fav" onclick="addFavorite()">加入收藏</button>`;

  document.getElementById('infoModal').classList.remove('hidden');
}

window.closeModal = function () {
  document.getElementById('infoModal').classList.add('hidden');
  currentListingId = null;
};

// ── Favorite actions ─────────────────────────────────────────────
window.addFavorite = async function () {
  await apiFetch(`/api/favorites/${currentListingId}`, 'POST');
  await loadView(activeView);
  closeModal();
};

window.removeFavorite = async function () {
  await apiFetch(`/api/favorites/${currentListingId}`, 'DELETE');
  await loadView(activeView);
  closeModal();
};

window.saveStatus = async function (status) {
  await apiFetch(`/api/favorites/${currentListingId}`, 'PATCH', { status });
};

window.saveNote = async function (note) {
  await apiFetch(`/api/favorites/${currentListingId}`, 'PATCH', { note });
};

// ── Marker highlight ─────────────────────────────────────────────
function highlightMarker(listing) {
  if (activeMarkerId) {
    const prev = markerMap.get(activeMarkerId);
    if (prev) {
      const isFav = favoriteIds.has(prev.listing.id);
      prev.marker.setIcon(makeCircleIcon(isFav ? '#10b981' : '#6366f1', false));
    }
  }
  activeMarkerId = listing.id;
  const entry = markerMap.get(listing.id);
  if (entry) {
    const isFav = favoriteIds.has(listing.id);
    entry.marker.setIcon(makeCircleIcon(isFav ? '#10b981' : '#6366f1', true));
  }
}

// ── Tooltip toggle ───────────────────────────────────────────────
window.toggleTooltips = function (visible) {
  tooltipsVisible = visible;
  markers.forEach(m => m.getTooltip()?.setOpacity(visible ? 0.9 : 0));
};

// ── API helper ───────────────────────────────────────────────────
async function apiFetch(url, method = 'GET', body = null) {
  const options = { method, headers: { 'Content-Type': 'application/json' } };
  if (body) options.body = JSON.stringify(body);
  const res = await fetch(url, options);
  if (method === 'GET') return res.json();
  return res;
}

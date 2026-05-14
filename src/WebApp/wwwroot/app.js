let map;
let markers = [];
let currentListings = [];
let currentFavorites = [];
let favoriteIds = new Set();
let activeView = 'favorites';
let currentListingId = null;

// ── Map init (called by Google Maps API callback) ────────────────
window.initMap = function () {
  map = new google.maps.Map(document.getElementById('map'), {
    center: { lat: 25.033, lng: 121.565 },
    zoom: 13,
    styles: darkMapStyles()
  });
  loadView('favorites');
};

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
  const params = new URLSearchParams();
  if (document.getElementById('filterFurniture').checked) params.set('hasFurniture', 'true');
  if (document.getElementById('filterInternet').checked) params.set('hasInternet', 'true');
  if (document.getElementById('filterGas').checked) params.set('hasNaturalGas', 'true');
  if (document.getElementById('filterParking').checked) params.set('hasParking', 'true');
  if (document.getElementById('filterPet').checked) params.set('petAllowed', 'true');
  const maxPrice = document.getElementById('filterMaxPrice').value;
  if (maxPrice) params.set('maxPrice', maxPrice);

  const listings = await apiFetch('/api/listings?' + params.toString());
  currentListings = listings;
  clearMarkers();
  renderList(listings, currentFavorites);
  renderMarkers(listings);
};

// ── Render markers ───────────────────────────────────────────────
function renderMarkers(listings) {
  listings.forEach(listing => {
    if (!listing.lat || !listing.lng) return;

    const isFav = favoriteIds.has(listing.id);
    const marker = new google.maps.Marker({
      position: { lat: listing.lat, lng: listing.lng },
      map,
      title: listing.title,
      icon: {
        path: google.maps.SymbolPath.CIRCLE,
        scale: 9,
        fillColor: isFav ? '#10b981' : '#6366f1',
        fillOpacity: 1,
        strokeColor: '#fff',
        strokeWeight: 2
      }
    });

    marker.addListener('click', () => openModal(listing));
    markers.push(marker);
  });
}

function clearMarkers() {
  markers.forEach(m => m.setMap(null));
  markers = [];
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
      openModal(listing, fav);
      if (listing.lat && listing.lng)
        map.panTo({ lat: listing.lat, lng: listing.lng });
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

  const b = v => v ? '✅' : '❌';
  document.getElementById('modalAmenities').innerHTML = `
    🪑 家具 ${b(listing.has_furniture)} &nbsp; 🔥 天然氣 ${b(listing.has_natural_gas)}<br>
    📺 第四台 ${b(listing.has_cable_tv)} &nbsp; 🌐 網路 ${b(listing.has_internet)}<br>
    🚗 停車 ${b(listing.has_parking)} &nbsp; 🐾 寵物 ${b(listing.pet_allowed)}
  `;

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

// ── API helper ───────────────────────────────────────────────────
async function apiFetch(url, method = 'GET', body = null) {
  const options = {
    method,
    headers: { 'Content-Type': 'application/json' }
  };
  if (body) options.body = JSON.stringify(body);
  const res = await fetch(url, options);
  if (method === 'GET') return res.json();
  return res;
}

// ── Dark map style ───────────────────────────────────────────────
function darkMapStyles() {
  return [
    { elementType: 'geometry', stylers: [{ color: '#1d2c4d' }] },
    { elementType: 'labels.text.fill', stylers: [{ color: '#8ec3b9' }] },
    { elementType: 'labels.text.stroke', stylers: [{ color: '#1a3646' }] },
    { featureType: 'road', elementType: 'geometry', stylers: [{ color: '#304a7d' }] },
    { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#0e1626' }] }
  ];
}

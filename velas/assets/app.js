const WHATSAPP = "573000000000";
const CLAVE_CARRITO = "luz-abundancia-carrito";

const PRODUCTOS = [
  {
    id: "canela-naranja",
    nombre: "Canela & Naranja",
    intencion: "Abundancia",
    notas: "Canela, naranja dulce, clavo",
    precio: 58000,
    cera: "#e9c77b",
    etiqueta: "#c08a2e",
  },
  {
    id: "laurel-menta",
    nombre: "Laurel & Menta",
    intencion: "Nuevos comienzos",
    notas: "Laurel, menta, limón",
    precio: 54000,
    cera: "#dfe6cf",
    etiqueta: "#5f7a5c",
  },
  {
    id: "albahaca-bergamota",
    nombre: "Albahaca & Bergamota",
    intencion: "Enfoque y claridad",
    notas: "Albahaca, bergamota, romero",
    precio: 54000,
    cera: "#eef0e2",
    etiqueta: "#2f4a35",
  },
  {
    id: "sandalo-vainilla",
    nombre: "Sándalo & Vainilla",
    intencion: "Gratitud",
    notas: "Sándalo, vainilla, ámbar",
    precio: 62000,
    cera: "#f3e3c8",
    etiqueta: "#8a5a2b",
  },
];

const formatoCOP = new Intl.NumberFormat("es-CO", {
  style: "currency",
  currency: "COP",
  maximumFractionDigits: 0,
});

function leerCarrito() {
  try {
    return JSON.parse(localStorage.getItem(CLAVE_CARRITO)) || {};
  } catch {
    return {};
  }
}

function guardarCarrito(carrito) {
  try {
    localStorage.setItem(CLAVE_CARRITO, JSON.stringify(carrito));
  } catch {
    /* Sin almacenamiento disponible: el carrito vive solo en memoria */
  }
}

let carrito = leerCarrito();

function svgVela({ cera, etiqueta }) {
  return `
    <svg viewBox="0 0 120 160" class="h-44 w-auto" aria-hidden="true">
      <ellipse cx="60" cy="150" rx="44" ry="6" fill="#000" opacity=".08"/>
      <path class="llama" d="M60 18c7 10 9 17 0 28-9-11-7-18 0-28z" fill="#f2a93b"/>
      <path class="llama" d="M60 28c3 5 4 9 0 14-4-5-3-9 0-14z" fill="#fff3c4"/>
      <rect x="58.5" y="44" width="3" height="10" rx="1.5" fill="#2a2622"/>
      <rect x="26" y="52" width="68" height="96" rx="10" fill="${cera}"/>
      <rect x="26" y="52" width="68" height="12" rx="6" fill="#fff" opacity=".35"/>
      <rect x="34" y="88" width="52" height="34" rx="4" fill="${etiqueta}"/>
      <circle cx="60" cy="105" r="7" fill="none" stroke="#faf6ee" stroke-width="1.5"/>
    </svg>`;
}

function renderProductos() {
  const contenedor = document.getElementById("productos");
  contenedor.innerHTML = PRODUCTOS.map(
    (p) => `
    <article class="group flex flex-col rounded-3xl bg-white p-6 shadow-sm ring-1 ring-arena transition hover:-translate-y-1 hover:shadow-lg">
      <div class="flex justify-center rounded-2xl bg-arena/60 py-6">${svgVela(p)}</div>
      <p class="mt-5 text-xs font-semibold tracking-widest text-oro uppercase">${p.intencion}</p>
      <h3 class="mt-1 font-display text-2xl font-semibold text-bosque">${p.nombre}</h3>
      <p class="mt-1 text-sm text-carbon/70">${p.notas}</p>
      <div class="mt-auto flex items-center justify-between pt-6">
        <span class="text-lg font-semibold">${formatoCOP.format(p.precio)}</span>
        <button type="button" class="btn-primario px-4 py-2" data-agregar="${p.id}" aria-label="Agregar ${p.nombre} al carrito">Agregar</button>
      </div>
    </article>`
  ).join("");
}

function renderCarrito() {
  const lista = document.getElementById("carrito-lista");
  const total = document.getElementById("carrito-total");
  const contador = document.getElementById("carrito-contador");
  const pagar = document.getElementById("carrito-pagar");

  const items = Object.entries(carrito)
    .map(([id, cantidad]) => ({ ...PRODUCTOS.find((p) => p.id === id), cantidad }))
    .filter((i) => i.id);

  const unidades = items.reduce((s, i) => s + i.cantidad, 0);
  const suma = items.reduce((s, i) => s + i.cantidad * i.precio, 0);

  contador.textContent = unidades;
  contador.classList.toggle("hidden", unidades === 0);
  total.textContent = formatoCOP.format(suma);
  pagar.toggleAttribute("disabled", unidades === 0);
  pagar.classList.toggle("opacity-50", unidades === 0);

  lista.innerHTML = items.length
    ? items
        .map(
          (i) => `
      <li class="flex items-center justify-between gap-4 py-4">
        <div>
          <p class="font-medium">${i.nombre}</p>
          <p class="text-sm text-carbon/60">${formatoCOP.format(i.precio)}</p>
        </div>
        <div class="flex items-center gap-2">
          <button type="button" class="size-8 rounded-full ring-1 ring-arena hover:bg-arena" data-restar="${i.id}" aria-label="Quitar una unidad de ${i.nombre}">-</button>
          <span class="w-6 text-center" aria-live="polite">${i.cantidad}</span>
          <button type="button" class="size-8 rounded-full ring-1 ring-arena hover:bg-arena" data-agregar="${i.id}" aria-label="Agregar una unidad de ${i.nombre}">+</button>
        </div>
      </li>`
        )
        .join("")
    : `<li class="py-10 text-center text-carbon/60">Tu carrito está vacío.</li>`;
}

function cambiarCantidad(id, delta) {
  const nueva = (carrito[id] || 0) + delta;
  if (nueva <= 0) delete carrito[id];
  else carrito[id] = nueva;
  guardarCarrito(carrito);
  renderCarrito();
}

function abrirCarrito(abrir) {
  const panel = document.getElementById("carrito");
  panel.classList.toggle("translate-x-full", !abrir);
  panel.setAttribute("aria-hidden", String(!abrir));
  document.getElementById("carrito-fondo").classList.toggle("hidden", !abrir);
  if (abrir) panel.querySelector("button").focus();
}

function pedirPorWhatsApp() {
  const lineas = Object.entries(carrito).map(([id, cantidad]) => {
    const p = PRODUCTOS.find((x) => x.id === id);
    return `- ${cantidad} x ${p.nombre} (${formatoCOP.format(p.precio * cantidad)})`;
  });
  const total = document.getElementById("carrito-total").textContent;
  const mensaje = `Hola, quiero hacer este pedido:\n${lineas.join("\n")}\nTotal: ${total}`;
  window.open(`https://wa.me/${WHATSAPP}?text=${encodeURIComponent(mensaje)}`, "_blank", "noopener");
}

document.addEventListener("click", (e) => {
  const agregar = e.target.closest("[data-agregar]");
  const restar = e.target.closest("[data-restar]");
  if (agregar) {
    cambiarCantidad(agregar.dataset.agregar, 1);
    if (!agregar.closest("#carrito")) abrirCarrito(true);
  }
  if (restar) cambiarCantidad(restar.dataset.restar, -1);
  if (e.target.closest("[data-abrir-carrito]")) abrirCarrito(true);
  if (e.target.closest("[data-cerrar-carrito]")) abrirCarrito(false);
});

document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") abrirCarrito(false);
});

document.getElementById("carrito-pagar").addEventListener("click", pedirPorWhatsApp);
document.getElementById("anio").textContent = new Date().getFullYear();

renderProductos();
renderCarrito();

# Luz de Abundancia

Landing page de venta de velas aromáticas ecológicas con enfoque en prosperidad. HTML estático, Tailwind CSS v4 compilado y JavaScript sin dependencias.

## Estructura
- `index.html`: página (hero, colección, ritual, valores ecológicos, FAQ, CTA).
- `assets/app.js`: catálogo, carrito persistente y pedido por WhatsApp.
- `src/input.css`: tema (colores, tipografías) y componentes de Tailwind.
- `assets/styles.css`: CSS compilado, no editar a mano.

## Desarrollo
```bash
npm install
npm run dev     # recompila al guardar
npm run build   # CSS minificado para producción
```

## Antes de publicar
- Reemplazar `WHATSAPP` en `assets/app.js` por el número real (formato internacional, sin `+`).
- Ajustar precios, aromas y duración en `PRODUCTOS` según el producto real.
- Sustituir las ilustraciones SVG por fotos reales si se desea.

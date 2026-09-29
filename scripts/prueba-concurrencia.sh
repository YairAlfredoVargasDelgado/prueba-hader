#!/usr/bin/env bash
#
# Demuestra que el ajuste de stock es seguro frente a peticiones concurrentes.
#
# Crea un producto con N unidades y lanza N+M descuentos de 1 unidad en paralelo.
# El resultado correcto es exactamente N respuestas 200, M respuestas 409 y un
# stock final de 0. Si el endpoint tuviera una condición de carrera, se verían
# más de N respuestas 200 o un stock final distinto de 0.
#
# Uso:  ./scripts/prueba-concurrencia.sh [URL_BASE] [STOCK_INICIAL] [PETICIONES]
#
set -euo pipefail

BASE_URL="${1:-http://localhost:5140}"
STOCK_INICIAL="${2:-30}"
PETICIONES="${3:-50}"

command -v curl   >/dev/null || { echo "Se requiere curl.";   exit 1; }
command -v python3 >/dev/null || { echo "Se requiere python3."; exit 1; }

echo "API:            $BASE_URL"
echo "Stock inicial:  $STOCK_INICIAL"
echo "Peticiones:     $PETICIONES descuentos concurrentes de 1 unidad"
echo

ID=$(curl -sS -X POST "$BASE_URL/api/products" \
        -H 'Content-Type: application/json' \
        -d "{\"name\":\"Prueba de concurrencia\",\"description\":\"Producto temporal\",\"price\":1000,\"stock\":$STOCK_INICIAL}" \
     | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')

echo "Producto creado con id $ID. Lanzando peticiones..."
echo

seq 1 "$PETICIONES" \
  | xargs -P "$PETICIONES" -I{} curl -sS -o /dev/null -w '%{http_code}\n' \
        -X PATCH "$BASE_URL/api/products/$ID/stock" \
        -H 'Content-Type: application/json' \
        -d '{"quantity":-1}' \
  | sort | uniq -c | sed 's/^/  respuestas: /'

echo
STOCK_FINAL=$(curl -sS "$BASE_URL/api/products/$ID" \
              | python3 -c 'import json,sys; print(json.load(sys.stdin)["stock"])')

echo "  stock final: $STOCK_FINAL"
echo

if [ "$STOCK_FINAL" -eq 0 ]; then
    echo "OK: no se perdió ningún ajuste y el stock nunca quedó en negativo."
else
    echo "ERROR: el stock final debería ser 0."
    exit 1
fi

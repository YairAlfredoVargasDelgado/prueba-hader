-- Esquema del catálogo de productos.
-- El script es idempotente: puede ejecutarse en cada arranque sin efectos
-- secundarios, lo que permite levantar el proyecto desde cero sin pasos manuales.

CREATE TABLE IF NOT EXISTS products (
    id          INT           NOT NULL AUTO_INCREMENT,
    name        VARCHAR(150)  NOT NULL,
    description VARCHAR(500)  NOT NULL DEFAULT '',
    price       DECIMAL(12,2) NOT NULL,
    stock       INT           NOT NULL,
    created_at  DATETIME(6)   NOT NULL,
    updated_at  DATETIME(6)   NOT NULL,
    PRIMARY KEY (id),
    KEY ix_products_name (name),
    -- Última línea de defensa: aunque el dominio ya lo impide, la base de datos
    -- tampoco acepta precios ni existencias negativas.
    CONSTRAINT ck_products_price_non_negative CHECK (price >= 0),
    CONSTRAINT ck_products_stock_non_negative CHECK (stock >= 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;

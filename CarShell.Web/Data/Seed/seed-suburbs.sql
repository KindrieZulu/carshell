-- Reference data for the Suburbs table: Zimbabwe's major cities and a
-- selection of well-known suburbs, replacing the UK-postcode-based location
-- model. Coordinates are approximate city/suburb-centre points, not precise
-- addresses — good enough for radius search, not for turn-by-turn routing.
-- Idempotent: safe to re-run.
-- Run with: psql -U <user> -h <host> -d carshell -f Data/Seed/seed-suburbs.sql

INSERT INTO "Suburbs" ("City", "Name", "Lat", "Lng") VALUES
    ('Harare', 'CBD', -17.8292, 31.0522),
    ('Harare', 'Avondale', -17.8009, 31.0342),
    ('Harare', 'Borrowdale', -17.7614, 31.0975),
    ('Harare', 'Mount Pleasant', -17.7756, 31.0447),
    ('Harare', 'Highlands', -17.7833, 31.0833),
    ('Harare', 'Newlands', -17.8083, 31.0667),
    ('Harare', 'Eastlea', -17.8194, 31.0664),
    ('Harare', 'Mabelreign', -17.8000, 31.0167),
    ('Harare', 'Mbare', -17.8564, 31.0297),
    ('Harare', 'Waterfalls', -17.8833, 31.0333),
    ('Chitungwiza', 'CBD', -18.0128, 31.0756),
    ('Bulawayo', 'CBD', -20.1500, 28.5833),
    ('Bulawayo', 'Hillside', -20.1833, 28.6167),
    ('Bulawayo', 'Suburbs', -20.1667, 28.5667),
    ('Bulawayo', 'Kumalo', -20.1764, 28.6114),
    ('Bulawayo', 'Northend', -20.1333, 28.5833),
    ('Mutare', 'CBD', -18.9707, 32.6709),
    ('Gweru', 'CBD', -19.4500, 29.8167),
    ('Kwekwe', 'CBD', -18.9281, 29.8149),
    ('Kadoma', 'CBD', -18.3333, 29.9167),
    ('Masvingo', 'CBD', -20.0637, 30.8277),
    ('Chinhoyi', 'CBD', -17.3667, 30.2000),
    ('Marondera', 'CBD', -18.1853, 31.5514),
    ('Victoria Falls', 'CBD', -17.9243, 25.8572),
    ('Bindura', 'CBD', -17.3019, 31.3306),
    ('Beitbridge', 'CBD', -22.2167, 30.0000)
ON CONFLICT ("City", "Name") DO NOTHING;

-- Reference data for the Suburbs table: cities, towns, growth points, and
-- suburbs across all ten of Zimbabwe's provinces, replacing the UK-postcode
-- based location model. Coordinates are approximate city/suburb-centre
-- points, not precise addresses — good enough for radius search, not for
-- turn-by-turn routing.
-- Idempotent: safe to re-run.
-- Run with: psql -U <user> -h <host> -d carshell -f Data/Seed/seed-suburbs.sql

INSERT INTO "Suburbs" ("City", "Name", "Lat", "Lng") VALUES
    -- Harare (city + suburbs)
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
    ('Harare', 'Belvedere', -17.8364, 31.0208),
    ('Harare', 'Milton Park', -17.8214, 31.0342),
    ('Harare', 'Alexandra Park', -17.7975, 31.0450),
    ('Harare', 'Marlborough', -17.7833, 30.9833),
    ('Harare', 'Msasa', -17.8500, 31.1167),
    ('Harare', 'Graniteside', -17.8500, 31.0500),
    ('Harare', 'Southerton', -17.8667, 31.0167),
    ('Harare', 'Hatfield', -17.8833, 31.0833),
    ('Harare', 'Workington', -17.8500, 30.9833),
    ('Harare', 'Warren Park', -17.8167, 30.9500),
    ('Harare', 'Kuwadzana', -17.8167, 30.9333),
    ('Harare', 'Glen View', -17.8833, 30.9667),
    ('Harare', 'Glen Norah', -17.8667, 31.0000),
    ('Harare', 'Budiriro', -17.8500, 30.9333),
    ('Harare', 'Highfield', -17.8667, 30.9833),
    ('Harare', 'Sunningdale', -17.8667, 31.0333),
    ('Harare', 'Dzivarasekwa', -17.7667, 30.9333),
    ('Harare', 'Mufakose', -17.8500, 30.9667),
    ('Harare', 'Kambuzuma', -17.8333, 30.9667),
    ('Harare', 'Tafara', -17.8333, 31.1333),
    ('Harare', 'Mabvuku', -17.8333, 31.1333),
    ('Harare', 'Greendale', -17.8000, 31.1000),
    ('Harare', 'Chisipite', -17.7833, 31.1000),
    ('Harare', 'Vainona', -17.7667, 31.0500),
    ('Harare', 'Emerald Hill', -17.7833, 31.0167),
    ('Harare', 'Belgravia', -17.8083, 31.0417),
    ('Harare', 'Hatcliffe', -17.7167, 31.0833),

    -- Harare metro (satellite towns)
    ('Chitungwiza', 'CBD', -18.0128, 31.0756),
    ('Chitungwiza', 'Zengeza', -18.0333, 31.0833),
    ('Chitungwiza', 'Seke', -18.0667, 31.1000),
    ('Chitungwiza', 'St Marys', -18.0000, 31.0667),
    ('Epworth', 'CBD', -17.8908, 31.1478),
    ('Norton', 'CBD', -17.8833, 30.7000),
    ('Ruwa', 'CBD', -17.8903, 31.2436),
    ('Goromonzi', 'CBD', -17.7833, 31.4167),

    -- Bulawayo (city + suburbs)
    ('Bulawayo', 'CBD', -20.1500, 28.5833),
    ('Bulawayo', 'Hillside', -20.1833, 28.6167),
    ('Bulawayo', 'Suburbs', -20.1667, 28.5667),
    ('Bulawayo', 'Kumalo', -20.1764, 28.6114),
    ('Bulawayo', 'Northend', -20.1333, 28.5833),
    ('Bulawayo', 'Nketa', -20.2000, 28.5333),
    ('Bulawayo', 'Pumula', -20.2167, 28.5667),
    ('Bulawayo', 'Njube', -20.2000, 28.5833),
    ('Bulawayo', 'Magwegwe', -20.1333, 28.5333),
    ('Bulawayo', 'Mzilikazi', -20.1667, 28.5667),
    ('Bulawayo', 'Entumbane', -20.1333, 28.5500),
    ('Bulawayo', 'Luveve', -20.1333, 28.5500),
    ('Bulawayo', 'Nkulumane', -20.1833, 28.5333),
    ('Bulawayo', 'Cowdray Park', -20.2333, 28.5500),
    ('Bulawayo', 'Khumalo', -20.1833, 28.6000),
    ('Bulawayo', 'Burnside', -20.1833, 28.6333),
    ('Bulawayo', 'Famona', -20.1667, 28.6000),
    ('Bulawayo', 'Selborne Park', -20.1667, 28.6167),
    ('Bulawayo', 'Riverside', -20.1500, 28.5333),

    -- Manicaland
    ('Mutare', 'CBD', -18.9707, 32.6709),
    ('Rusape', 'CBD', -18.5333, 32.1167),
    ('Chipinge', 'CBD', -20.1883, 32.6236),
    ('Chimanimani', 'CBD', -19.8000, 32.8667),
    ('Nyanga', 'CBD', -18.2167, 32.7500),
    ('Buhera', 'CBD', -19.3167, 31.4333),
    ('Mutasa', 'CBD', -18.6167, 32.6833),

    -- Mashonaland Central
    ('Bindura', 'CBD', -17.3019, 31.3306),
    ('Mount Darwin', 'CBD', -16.7667, 31.5833),
    ('Guruve', 'CBD', -16.6667, 30.7000),
    ('Shamva', 'CBD', -17.3167, 31.5667),
    ('Mvurwi', 'CBD', -17.0333, 30.8500),
    ('Centenary', 'CBD', -16.8000, 31.1167),
    ('Concession', 'CBD', -17.3667, 30.9833),
    ('Rushinga', 'CBD', -16.7333, 32.1667),

    -- Mashonaland East
    ('Marondera', 'CBD', -18.1853, 31.5514),
    ('Murehwa', 'CBD', -17.6500, 31.7833),
    ('Mutoko', 'CBD', -17.4000, 32.2333),
    ('Macheke', 'CBD', -18.1500, 31.8500),
    ('Wedza', 'CBD', -18.6167, 31.5667),
    ('Chivhu', 'CBD', -19.0167, 30.9000),
    ('Mudzi', 'CBD', -17.0000, 32.4333),

    -- Mashonaland West
    ('Chinhoyi', 'CBD', -17.3667, 30.2000),
    ('Kariba', 'CBD', -16.5167, 28.8000),
    ('Chegutu', 'CBD', -18.1333, 30.1500),
    ('Kadoma', 'CBD', -18.3333, 29.9167),
    ('Karoi', 'CBD', -16.8167, 29.6833),
    ('Banket', 'CBD', -17.3833, 30.4000),
    ('Zvimba', 'CBD', -17.6167, 30.3167),

    -- Masvingo
    ('Masvingo', 'CBD', -20.0637, 30.8277),
    ('Chiredzi', 'CBD', -21.0500, 31.6667),
    ('Zaka', 'CBD', -20.3333, 31.4333),
    ('Gutu', 'CBD', -19.6667, 31.1500),
    ('Bikita', 'CBD', -19.9167, 31.5833),
    ('Mwenezi', 'CBD', -21.3667, 30.7333),
    ('Triangle', 'CBD', -21.0333, 31.4833),
    ('Chatsworth', 'CBD', -19.7667, 30.5333),

    -- Matabeleland North
    ('Hwange', 'CBD', -18.3667, 26.5000),
    ('Victoria Falls', 'CBD', -17.9243, 25.8572),
    ('Lupane', 'CBD', -18.9333, 27.8000),
    ('Binga', 'CBD', -17.6167, 27.3500),
    ('Nkayi', 'CBD', -19.0000, 28.9000),
    ('Tsholotsho', 'CBD', -19.7500, 27.7667),

    -- Matabeleland South
    ('Gwanda', 'CBD', -20.9333, 29.0000),
    ('Beitbridge', 'CBD', -22.2167, 30.0000),
    ('Plumtree', 'CBD', -20.4833, 27.8167),
    ('Filabusi', 'CBD', -20.5333, 29.2833),
    ('Esigodini', 'CBD', -20.3167, 28.9333),

    -- Midlands
    ('Gweru', 'CBD', -19.4500, 29.8167),
    ('Kwekwe', 'CBD', -18.9281, 29.8149),
    ('Zvishavane', 'CBD', -20.3333, 30.0667),
    ('Shurugwi', 'CBD', -19.6667, 30.0000),
    ('Redcliff', 'CBD', -19.0333, 29.7833),
    ('Gokwe', 'CBD', -18.2167, 28.9333),
    ('Mvuma', 'CBD', -19.2833, 30.5167)
ON CONFLICT ("City", "Name") DO NOTHING;

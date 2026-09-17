-- Reference data for the Makes/VehicleModels tables: common UK-market
-- makes and their mainstream models, enough for search filters and the
-- listing form to be usable. Idempotent: safe to re-run.
-- Run with: psql -U <user> -h <host> -d carshell -f Data/Seed/seed-makes-models.sql

INSERT INTO "Makes" ("Name") VALUES
    ('Ford'), ('Vauxhall'), ('Volkswagen'), ('BMW'), ('Audi'),
    ('Mercedes-Benz'), ('Toyota'), ('Nissan'), ('Peugeot'), ('Renault'),
    ('Kia'), ('Hyundai'), ('Skoda'), ('SEAT'), ('Honda'),
    ('Mazda'), ('Volvo'), ('Land Rover'), ('Jaguar'), ('MINI'),
    ('Fiat'), ('Citroen'), ('Suzuki'), ('Tesla'), ('Porsche'), ('Lexus')
ON CONFLICT ("Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Fiesta'), ('Focus'), ('Kuga'), ('Puma'), ('Mondeo'), ('EcoSport'), ('Galaxy')) AS v(name) ON true
WHERE m."Name" = 'Ford'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Corsa'), ('Astra'), ('Insignia'), ('Mokka'), ('Crossland'), ('Grandland')) AS v(name) ON true
WHERE m."Name" = 'Vauxhall'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Golf'), ('Polo'), ('Tiguan'), ('Passat'), ('T-Roc'), ('ID.3'), ('ID.4')) AS v(name) ON true
WHERE m."Name" = 'Volkswagen'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('1 Series'), ('2 Series'), ('3 Series'), ('4 Series'), ('5 Series'), ('X1'), ('X3'), ('X5')) AS v(name) ON true
WHERE m."Name" = 'BMW'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('A1'), ('A3'), ('A4'), ('A6'), ('Q2'), ('Q3'), ('Q5'), ('Q7')) AS v(name) ON true
WHERE m."Name" = 'Audi'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('A-Class'), ('C-Class'), ('E-Class'), ('GLA'), ('GLC'), ('GLE')) AS v(name) ON true
WHERE m."Name" = 'Mercedes-Benz'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Aygo'), ('Yaris'), ('Corolla'), ('C-HR'), ('RAV4'), ('Prius')) AS v(name) ON true
WHERE m."Name" = 'Toyota'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Micra'), ('Qashqai'), ('Juke'), ('Leaf'), ('X-Trail')) AS v(name) ON true
WHERE m."Name" = 'Nissan'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('108'), ('208'), ('308'), ('2008'), ('3008'), ('5008')) AS v(name) ON true
WHERE m."Name" = 'Peugeot'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Clio'), ('Captur'), ('Megane'), ('Kadjar'), ('Zoe')) AS v(name) ON true
WHERE m."Name" = 'Renault'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Picanto'), ('Rio'), ('Ceed'), ('Sportage'), ('Niro'), ('Stonic')) AS v(name) ON true
WHERE m."Name" = 'Kia'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('i10'), ('i20'), ('i30'), ('Tucson'), ('Kona'), ('Santa Fe')) AS v(name) ON true
WHERE m."Name" = 'Hyundai'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Fabia'), ('Octavia'), ('Superb'), ('Kodiaq'), ('Karoq')) AS v(name) ON true
WHERE m."Name" = 'Skoda'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Ibiza'), ('Leon'), ('Arona'), ('Ateca')) AS v(name) ON true
WHERE m."Name" = 'SEAT'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Jazz'), ('Civic'), ('CR-V'), ('HR-V')) AS v(name) ON true
WHERE m."Name" = 'Honda'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('2'), ('3'), ('6'), ('CX-3'), ('CX-5')) AS v(name) ON true
WHERE m."Name" = 'Mazda'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('XC40'), ('XC60'), ('XC90'), ('S60'), ('V60')) AS v(name) ON true
WHERE m."Name" = 'Volvo'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Defender'), ('Discovery'), ('Discovery Sport'), ('Range Rover'), ('Range Rover Evoque'), ('Range Rover Sport')) AS v(name) ON true
WHERE m."Name" = 'Land Rover'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('XE'), ('XF'), ('F-Pace'), ('E-Pace'), ('I-Pace')) AS v(name) ON true
WHERE m."Name" = 'Jaguar'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Hatch'), ('Convertible'), ('Countryman'), ('Clubman')) AS v(name) ON true
WHERE m."Name" = 'MINI'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('500'), ('Panda'), ('Tipo')) AS v(name) ON true
WHERE m."Name" = 'Fiat'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('C1'), ('C3'), ('C4'), ('C5 Aircross')) AS v(name) ON true
WHERE m."Name" = 'Citroen'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Swift'), ('Vitara'), ('Ignis'), ('S-Cross')) AS v(name) ON true
WHERE m."Name" = 'Suzuki'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('Model 3'), ('Model S'), ('Model X'), ('Model Y')) AS v(name) ON true
WHERE m."Name" = 'Tesla'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('911'), ('718 Cayman'), ('718 Boxster'), ('Cayenne'), ('Macan'), ('Panamera'), ('Taycan')) AS v(name) ON true
WHERE m."Name" = 'Porsche'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name")
SELECT m."Id", v.name
FROM "Makes" m
JOIN (VALUES ('CT'), ('IS'), ('ES'), ('NX'), ('RX')) AS v(name) ON true
WHERE m."Name" = 'Lexus'
ON CONFLICT ("MakeId", "Name") DO NOTHING;

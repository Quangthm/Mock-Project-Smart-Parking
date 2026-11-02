-- Optional, existing public demo fixture only. No other user's password is changed.
-- Run after 05.2 if the old demo seed still stores the known plaintext password.
CREATE EXTENSION IF NOT EXISTS pgcrypto;
UPDATE users
SET password_hash = crypt('Password@123', gen_salt('bf', 12))
WHERE id = '11111111-1111-1111-1111-111111111111'
  AND email = 'driver@gmail.com'
  AND password_hash = 'Password@123';

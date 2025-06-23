1. Run alpine linux connected to the open-webui volume. /path/to/data depends on your volume settings.

	podman run -it --rm -v open-webui:/path/to/data alpine

2. Install apache2-utils and sqlite:

	apk add apache2-utils sqlite

3. Generate bcrypt hash:

	htpasswd -bnBC 10 "" your-new-password | tr -d ':'

4. Open SQLite Database

sqlite3 /path/to/data/webui.db

5. Select current records:

SELECT * FROM auth;

6. Update password:

UPDATE auth SET password='<insert_hash_here>' WHERE email='admin@example.com';

7. exit sqlite, press [Ctrl + d] or type:

	.exit
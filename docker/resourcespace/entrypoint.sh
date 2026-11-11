#!/bin/bash
set -e

# Restore persisted config.php if container was recreated with an existing filestore volume
if [ ! -f /var/www/html/include/config.php ] && [ -f /var/www/html/filestore/.persisted_config.php ]; then
    cp /var/www/html/filestore/.persisted_config.php /var/www/html/include/config.php
    chown www-data:www-data /var/www/html/include/config.php
fi

chown -R www-data:www-data /var/www/html/filestore

service cron start || true
chmod +x /etc/cron.daily/* || true

exec apachectl -D FOREGROUND

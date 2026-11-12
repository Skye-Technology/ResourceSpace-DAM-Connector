FROM ubuntu:24.04

LABEL org.opencontainers.image.authors="Skye Technology / Montala Ltd based"
LABEL org.opencontainers.image.description="Local ResourceSpace 11.0 DAM for Umbraco connector development"

ENV DEBIAN_FRONTEND="noninteractive"

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    imagemagick \
    apache2 \
    subversion \
    ghostscript \
    antiword \
    poppler-utils \
    libimage-exiftool-perl \
    cron \
    wget \
    curl \
    mariadb-client \
    php \
    php-apcu \
    php-curl \
    php-gd \
    php-intl \
    php-mysqlnd \
    php-mbstring \
    php-xml \
    php-zip \
    libapache2-mod-php \
    ffmpeg \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

RUN PHP_INI_DIR=$(php -r "echo PHP_CONFIG_FILE_SCAN_DIR;" | sed 's|/cli/conf.d||') \
 && sed -i -e "s/upload_max_filesize\s*=\s*2M/upload_max_filesize = 100M/g" "${PHP_INI_DIR}/apache2/php.ini" \
 && sed -i -e "s/post_max_size\s*=\s*8M/post_max_size = 100M/g" "${PHP_INI_DIR}/apache2/php.ini" \
 && sed -i -e "s/max_execution_time\s*=\s*30/max_execution_time = 300/g" "${PHP_INI_DIR}/apache2/php.ini" \
 && sed -i -e "s/memory_limit\s*=\s*128M/memory_limit = 512M/g" "${PHP_INI_DIR}/apache2/php.ini" \
 && (sed -i 's/rights="none" pattern="PDF"/rights="read|write" pattern="PDF"/g' /etc/ImageMagick-*/policy.xml || true)

RUN printf '<Directory /var/www/html>\n\tOptions FollowSymLinks\n\tAllowOverride All\n</Directory>\nServerName localhost\n' \
 >> /etc/apache2/sites-enabled/000-default.conf \
 && a2enmod rewrite headers

COPY cronjob /etc/cron.daily/resourcespace
RUN chmod +x /etc/cron.daily/resourcespace

WORKDIR /var/www/html

RUN rm -f index.html \
 && svn export --force -q https://svn.resourcespace.com/svn/rs/releases/11.0 . \
 && mkdir -p filestore \
 && chown -R root:www-data /var/www/html \
 && chmod -R 755 /var/www/html \
 && chown -R www-data:www-data /var/www/html/filestore /var/www/html/include \
 && chmod -R 775 /var/www/html/filestore /var/www/html/include

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80

CMD ["/entrypoint.sh"]

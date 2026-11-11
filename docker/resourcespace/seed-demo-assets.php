<?php
/**
 * Idempotent local bootstrap & sample asset seeder for ResourceSpace 11.0.
 * Intended strictly for local Docker development alongside Umbraco.
 */

ob_start();

$configPath = '/var/www/html/include/config.php';
$persistedConfigPath = '/var/www/html/filestore/.persisted_config.php';

$baseUrl = getenv('RS_BASE_URL') ?: 'http://localhost:8085';
$dbHost = getenv('MYSQL_HOST') ?: 'mariadb';
$dbName = getenv('MYSQL_DATABASE') ?: 'resourcespace';
$dbUser = getenv('MYSQL_USER') ?: 'resourcespace_rw';
$dbPass = getenv('MYSQL_PASSWORD') ?: 'rs_local_dev_pw';
$wroteConfig = false;

if (!file_exists($configPath) || filesize($configPath) < 50) {
    $configPhp = <<<PHP
<?php
\$mysql_server = '{$dbHost}';
\$mysql_username = '{$dbUser}';
\$mysql_password = '{$dbPass}';
\$mysql_db = '{$dbName}';

\$baseurl = '{$baseUrl}';
\$email_from = 'dam@localhost.invalid';
\$email_notify = 'dam@localhost.invalid';
\$applicationname = 'Civic Museum ResourceSpace DAM';

\$scramble_key = 'local_dev_rs_scramble_2026';
\$api_scramble_key = 'local_dev_rs_api_scramble_2026';
\$spider_password = 'local_spider_pw_2026';

\$enable_remote_apis = true;
\$upload_paths = ['/tmp/rs_demo_seed'];

\$imagemagick_path = '/usr/bin';
\$ghostscript_path = '/usr/bin';
\$ffmpeg_path = '/usr/bin';
\$exiftool_path = '/usr/bin';
\$antiword_path = '/usr/bin';
\$pdftotext_path = '/usr/bin';
PHP;

    file_put_contents($configPath, $configPhp);
    file_put_contents($persistedConfigPath, $configPhp);
    $wroteConfig = true;
}

// Ensure $upload_paths is available even if config.php was written prior to this setting
$upload_paths = ['/tmp/rs_demo_seed'];

$suppress_headers = true;
$nocache = false;
chdir('/var/www/html/include');
require_once '/var/www/html/include/boot.php';
$upload_paths = ['/tmp/rs_demo_seed'];
ob_end_flush();

if ($wroteConfig) {
    echo "[seed] Wrote ResourceSpace config.php ({$baseUrl}).\n";
}
require_once '/var/www/html/include/image_processing.php';
require_once '/var/www/html/include/resource_functions.php';
require_once '/var/www/html/include/api_functions.php';

// Ensure database schema and seed tables are up to date
CheckDBStruct(false);

// 1. Ensure 'admin' user exists with known local dev password ('admin123')
$adminRef = get_user_by_username('admin');
if ($adminRef === false) {
    $adminRef = new_user('admin', 3);
}
$adminHash = rs_password_hash('RSadmin' . 'admin123');
ps_query(
    'UPDATE user SET password = ?, fullname = ?, email = ?, usergroup = 3, approved = 1, account_expires = NULL WHERE ref = ?',
    ['s', $adminHash, 's', 'DAM Administrator', 's', 'admin@example.org', 'i', $adminRef]
);

// 2. Ensure dedicated 'umbraco_api' service user exists (usergroup 2 = General User, read-only DAM consumer)
$apiUsername = 'umbraco_api';
$apiUserRef = get_user_by_username($apiUsername);
if ($apiUserRef === false) {
    $apiUserRef = new_user($apiUsername, 2);
}
$apiUserPassHash = rs_password_hash("RS{$apiUsername}" . 'umbraco_local_api_pw');
ps_query(
    'UPDATE user SET password = ?, fullname = ?, email = ?, usergroup = 2, approved = 1, account_expires = NULL WHERE ref = ?',
    ['s', $apiUserPassHash, 's', 'Umbraco CMS Connector', 's', 'umbraco@example.org', 'i', $apiUserRef]
);

$apiKey = get_api_key($apiUserRef);

// Authenticate current CLI session as admin to ingest demo assets
$adminData = get_user($adminRef);
setup_user($adminData);

$existingWithFiles = (int) ps_value('SELECT COUNT(*) AS `value` FROM resource WHERE ref > 0 AND (no_file IS NULL OR no_file = 0)', [], 0);
if ($existingWithFiles === 0) {
    // Clean any incomplete placeholder rows before seeding
    ps_query('DELETE FROM resource WHERE ref > 0 AND no_file = 1', []);
    ps_query('DELETE FROM resource_node WHERE resource > 0 AND resource NOT IN (SELECT ref FROM resource)', []);
    ps_query('ALTER TABLE resource AUTO_INCREMENT = 1', []);

    echo "[seed] Generating and ingesting 4 sample DAM assets...\n";
    $tmpDir = '/tmp/rs_demo_seed';
    if (!is_dir($tmpDir)) {
        mkdir($tmpDir, 0775, true);
    }

    // Sample 1: Roman Floor Mosaic (Photo, Open)
    $img1 = "{$tmpDir}/roman-floor-mosaic.jpg";
    exec(sprintf(
        'convert -size 1600x1000 plasma:fractal -modulate 95,65,110 -blur 0x2 ' .
        '-fill "#1e293b" -draw "rectangle 60,760 1540,940" ' .
        '-fill "#f8fafc" -pointsize 48 -annotate +95+835 "Roman Floor Mosaic - North Wing" ' .
        '-fill "#cbd5e1" -pointsize 28 -annotate +95+895 "Civic Museum Collection  |  ResourceSpace DAM" %s',
        escapeshellarg($img1)
    ));
    seed_asset(
        1,
        0,
        0,
        $img1,
        'Roman Floor Mosaic, North Wing',
        'Fourth-century geometric Roman floor mosaic excavated in 1924 and displayed in the North Wing gallery.',
        'Trustees of the Civic Museum'
    );

    // Sample 2: Botanical Herbarium Plate (Photo, Open)
    $img2 = "{$tmpDir}/herbarium-rosa-canina.jpg";
    exec(sprintf(
        'convert -size 1400x1000 xc:"#f5f0e6" ' .
        '-fill "#2d5a27" -draw "circle 700,430 700,220" ' .
        '-fill "#be123c" -draw "circle 700,430 700,320" ' .
        '-fill "#1f2937" -draw "rectangle 60,760 1340,940" ' .
        '-fill "#fef3c7" -pointsize 44 -annotate +90+835 "Botanical Herbarium Plate: Rosa Canina" ' .
        '-fill "#d1d5db" -pointsize 26 -annotate +90+895 "University Special Collections  |  1889 Folio" %s',
        escapeshellarg($img2)
    ));
    seed_asset(
        1,
        0,
        0,
        $img2,
        'Botanical Herbarium Plate — Rosa Canina',
        'Pressed specimen and watercolour plate of Rosa canina from the 1889 University Herbarium folio.',
        'University Special Collections'
    );

    // Sample 3: Conservation Report (Document PDF, Open)
    // Generate via PostScript -> Ghostscript ps2pdf so it works regardless of ImageMagick PDF coder policy
    $ppm3 = "{$tmpDir}/report-cover.jpg";
    $pdf3 = "{$tmpDir}/annual-conservation-report-2026.pdf";
    exec(sprintf(
        'convert -size 1240x1754 xc:white ' .
        '-fill "#0f172a" -draw "rectangle 0,0 1240,260" ' .
        '-fill "#ffffff" -pointsize 48 -annotate +80+155 "2026 Annual Conservation Report" ' .
        '-fill "#334155" -pointsize 30 -annotate +80+380 "Heritage Conservation Trust - Public Archive Document" ' .
        '-fill "#64748b" -pointsize 24 -annotate +80+450 "Delivered directly from ResourceSpace without duplicating into Umbraco Media." %s',
        escapeshellarg($ppm3)
    ));
    exec(sprintf('img2pdf %s -o %s 2>/dev/null || gs -q -dNOPAUSE -dBATCH -sDEVICE=pdfwrite -sOutputFile=%s viewjpeg.ps -c "(%s) viewJPEG showpage"',
        escapeshellarg($ppm3),
        escapeshellarg($pdf3),
        escapeshellarg($pdf3),
        $ppm3
    ));
    seed_asset(
        2,
        0,
        0,
        file_exists($pdf3) && filesize($pdf3) > 100 ? $pdf3 : $ppm3,
        '2026 Annual Conservation Report (PDF)',
        'Annual report detailing textile, stone, and manuscript conservation work across regional collections.',
        'Heritage Conservation Trust'
    );

    // Sample 4: Embargoed Loan Artefact (Photo, RESTRICTED access = 1)
    $img4 = "{$tmpDir}/embargoed-loan-artefact.jpg";
    exec(sprintf(
        'convert -size 1200x900 xc:"#450a0a" ' .
        '-fill "#fca5a5" -pointsize 44 -annotate +80+430 "EMBARGOED LOAN ARTEFACT" ' .
        '-fill "#fecaca" -pointsize 28 -annotate +80+500 "Restricted Access (access=1) in ResourceSpace" %s',
        escapeshellarg($img4)
    ));
    seed_asset(
        1,
        0,
        1,
        $img4,
        'Embargoed Loan Artefact (Restricted)',
        'Restricted loan item used to verify that non-open DAM assets are blocked from public Umbraco delivery.',
        'Private Loan — Embargoed'
    );
} else {
    // Ensure denormalized resource.field8 column is populated on existing rows
    ps_query(
        'UPDATE resource r
         INNER JOIN resource_node rn ON rn.resource = r.ref
         INNER JOIN node n ON n.ref = rn.node AND n.resource_type_field = 8
         SET r.field8 = LEFT(n.name, 200)
         WHERE r.ref > 0 AND (r.field8 IS NULL OR r.field8 = \'\')',
        []
    );
    echo "[seed] ResourceSpace already contains {$existingWithFiles} live resources; skipping sample upload.\n";
}

echo "\n========================================================\n";
echo "ResourceSpace 11.0 Local DAM Ready!\n";
echo "  Web UI:        {$baseUrl}\n";
echo "  Admin Login:   admin / admin123\n";
echo "  API Username:  {$apiUsername}\n";
echo "  API Key:       {$apiKey}\n";
echo "========================================================\n";

function seed_asset(
    int $resourceType,
    int $archiveState,
    int $accessLevel,
    string $filePath,
    string $title,
    string $caption,
    string $credit
): int {
    global $adminRef;
    $ref = create_resource($resourceType, $archiveState, $adminRef);
    $ext = strtolower(pathinfo($filePath, PATHINFO_EXTENSION));

    $destPath = get_resource_path($ref, true, '', true, $ext);
    $copied = copy($filePath, $destPath);

    ps_query(
        'UPDATE resource SET access = ?, file_extension = ?, preview_extension = ?, file_modified = NOW(), no_file = 0 WHERE ref = ?',
        ['i', $accessLevel, 's', $ext, 's', 'jpg', 'i', $ref]
    );

    create_previews($ref, false, $ext);

    update_field($ref, 8, $title);
    ps_query(
        'UPDATE resource SET field8 = LEFT(?, 200) WHERE ref = ? AND (field8 IS NULL OR field8 = \'\')',
        ['s', $title, 'i', $ref]
    );
    update_field($ref, 18, $caption);
    if (ps_value('SELECT COUNT(*) AS `value` FROM resource_type_field WHERE ref = 10', [], 0) > 0) {
        update_field($ref, 10, $credit);
    }

    echo "[seed] Created resource #{$ref}: {$title} (ext={$ext}, stored=" . ($copied ? 'yes' : 'no') . ", access={$accessLevel})\n";
    return $ref;
}

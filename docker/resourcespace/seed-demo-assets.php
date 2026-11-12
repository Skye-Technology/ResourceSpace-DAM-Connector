<?php
/**
 * Idempotent local bootstrap & curated CC0 museum asset seeder for ResourceSpace 11.0.
 * Pass RS_FORCE_RESEED=1 (or `./seed-demo-assets.sh --reseed`) to replace existing demo assets.
 */

ob_start();

$configPath = '/var/www/html/include/config.php';
$persistedConfigPath = '/var/www/html/filestore/.persisted_config.php';

$baseUrl = getenv('RS_BASE_URL') ?: 'http://localhost:8085';
$dbHost = getenv('MYSQL_HOST') ?: 'mariadb';
$dbName = getenv('MYSQL_DATABASE') ?: 'resourcespace';
$dbUser = getenv('MYSQL_USER') ?: 'resourcespace_rw';
$dbPass = getenv('MYSQL_PASSWORD') ?: 'rs_local_dev_pw';
$forceReseed = (getenv('RS_FORCE_RESEED') === '1' || getenv('RS_FORCE_RESEED') === 'true');
$wroteConfig = false;

if (!file_exists($configPath) || filesize($configPath) < 50) {
    $exportedHost = var_export($dbHost, true);
    $exportedUser = var_export($dbUser, true);
    $exportedPass = var_export($dbPass, true);
    $exportedDb = var_export($dbName, true);
    $exportedBaseUrl = var_export($baseUrl, true);

    $configPhp = <<<PHP
<?php
\$mysql_server = {$exportedHost};
\$mysql_username = {$exportedUser};
\$mysql_password = {$exportedPass};
\$mysql_db = {$exportedDb};

\$baseurl = {$exportedBaseUrl};
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
    @chmod($configPath, 0640);
    file_put_contents($persistedConfigPath, $configPhp);
    @chmod($persistedConfigPath, 0640);
    $wroteConfig = true;
}

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

if ($forceReseed) {
    echo "[seed] RS_FORCE_RESEED=1 detected — purging previous demo resources and filestore previews...\n";
    ps_query('DELETE FROM resource_dimensions WHERE resource > 0', []);
    ps_query('DELETE FROM resource_node WHERE resource > 0', []);
    ps_query('DELETE FROM resource_keyword WHERE resource > 0', []);
    ps_query('DELETE FROM resource_alt_files WHERE resource > 0', []);
    ps_query('DELETE FROM resource WHERE ref > 0', []);
    ps_query('ALTER TABLE resource AUTO_INCREMENT = 1', []);

    foreach (glob('/var/www/html/filestore/[0-9]*') as $entry) {
        exec('rm -rf ' . escapeshellarg($entry));
    }
}

$existingWithFiles = (int) ps_value('SELECT COUNT(*) AS `value` FROM resource WHERE ref > 0 AND (no_file IS NULL OR no_file = 0)', [], 0);
if ($existingWithFiles === 0) {
    ps_query('DELETE FROM resource WHERE ref > 0 AND no_file = 1', []);
    ps_query('DELETE FROM resource_node WHERE resource > 0 AND resource NOT IN (SELECT ref FROM resource)', []);
    ps_query('ALTER TABLE resource AUTO_INCREMENT = 1', []);

    echo "[seed] Ingesting 8 curated CC0 museum assets into ResourceSpace...\n";
    $tmpDir = '/tmp/rs_demo_seed';
    if (!is_dir($tmpDir)) {
        mkdir($tmpDir, 0775, true);
    }

    // Resource #1: Roman Marine Floor Mosaic (Photo, Open)
    $img1 = ensure_photo_asset($tmpDir, '01-roman-marine-mosaic.jpg', 'Mosaic Floor Panel Depicting Marine Life');
    seed_asset(
        1,
        0,
        0,
        $img1,
        'Mosaic Floor Panel Depicting Marine Life',
        'Third-century Roman stone and mortar floor mosaic from the House of the Man of Letters at Daphne (Antioch), 200–230 CE.',
        'Art Institute of Chicago Open Access (CC0 Public Domain)'
    );

    // Resource #2: Botanical Rose Study — Fantin-Latour (Photo, Open)
    $img2 = ensure_photo_asset($tmpDir, '02-botanical-roses-plate.jpg', 'Botanical Study — Roses in a Bowl');
    seed_asset(
        1,
        0,
        0,
        $img2,
        'Botanical Study — Roses in a Bowl (1881)',
        'Oil study of heirloom garden roses in a dark stoneware bowl by Henri Fantin-Latour (1881), held in the botanical fine-art collection.',
        'Art Institute of Chicago Open Access (CC0 Public Domain)'
    );

    // Resource #3: Illustrated 2026 Annual Conservation Report (PDF Document, Open)
    $coverJpg = "{$tmpDir}/report-cover-illustrated.jpg";
    $mosaicBanner = "{$tmpDir}/report-mosaic-banner.jpg";
    $pdf3 = "{$tmpDir}/annual-conservation-report-2026.pdf";
    exec(sprintf(
        'convert %s -resize 1072x550^ -gravity center -extent 1072x550 %s',
        escapeshellarg($img1),
        escapeshellarg($mosaicBanner)
    ));
    exec(sprintf(
        'convert -size 1240x1754 xc:"#f8fafc" ' .
        '-fill "#0f172a" -draw "rectangle 0,0 1240,340" ' .
        '-fill "#3544b1" -draw "rectangle 0,340 1240,356" ' .
        '-fill "#cef373" -pointsize 24 -annotate +84+108 "CIVIC MUSEUM CONSERVATION DEPARTMENT  |  ANNUAL DOSSIER" ' .
        '-fill "#ffffff" -pointsize 54 -annotate +84+190 "2026 Conservation &" ' .
        '-fill "#ffffff" -pointsize 54 -annotate +84+258 "Collections Report" ' .
        '%s -geometry +84+430 -composite ' .
        '-fill "#0f172a" -pointsize 32 -annotate +84+1070 "Featured Treatment: Daphne Marine Tesserae Consolidation" ' .
        '-fill "#475569" -pointsize 24 -annotate +84+1130 "Covers structural mortar stabilisation, multispectral pigment imaging," ' .
        '-fill "#475569" -pointsize 24 -annotate +84+1168 "vellum deacidification, and zero-duplication digital archive delivery." ' .
        '-fill "#e2e8f0" -draw "rectangle 84,1250 1156,1560" ' .
        '-fill "#1e293b" -pointsize 24 -annotate +120+1320 "Archive Reference:  RS-DOC-2026-03" ' .
        '-fill "#1e293b" -pointsize 24 -annotate +120+1375 "Distribution:       Open Public Access (ResourceSpace DAM)" ' .
        '-fill "#1e293b" -pointsize 24 -annotate +120+1430 "License:            CC0 1.0 Public Domain Photography" ' .
        '-fill "#64748b" -pointsize 20 -annotate +120+1505 "Streamed directly from ResourceSpace 11.0 without copying into Umbraco Media." %s',
        escapeshellarg($mosaicBanner),
        escapeshellarg($coverJpg)
    ));
    exec(sprintf('convert %s -density 150 %s', escapeshellarg($coverJpg), escapeshellarg($pdf3)));
    seed_asset(
        2,
        0,
        0,
        file_exists($pdf3) && filesize($pdf3) > 1000 ? $pdf3 : $coverJpg,
        '2026 Annual Conservation Report (PDF)',
        'Illustrated annual conservation dossier covering Roman mosaic tesserae stabilisation, vellum manuscript imaging, and public archive stewardship.',
        'Civic Museum Conservation Trust'
    );

    // Resource #4: Portrait Head of Emperor Hadrian (Photo, RESTRICTED access = 1)
    $img4 = ensure_photo_asset($tmpDir, '04-hadrian-marble-bust-restricted.jpg', 'Portrait Head of Emperor Hadrian (Restricted Loan)');
    seed_asset(
        1,
        0,
        1,
        $img4,
        'Portrait Head of Emperor Hadrian (Restricted Loan)',
        'Second-century Roman marble portrait bust (130–138 CE) under embargoed loan governance (access=1); suppressed from public Umbraco delivery.',
        'Embargoed International Loan · CC0 Study Reference'
    );

    // Resource #5: Athenian Belly-Amphora (Photo, Open)
    $img5 = ensure_photo_asset($tmpDir, '05-athenian-amphora.jpg', 'Athenian Belly-Amphora (550–540 BCE)');
    seed_asset(
        1,
        0,
        0,
        $img5,
        'Athenian Belly-Amphora (550–540 BCE)',
        'Greek black-figure terracotta storage amphora depicting Herakles wrestling the Nemean Lion, flanked by Athena and Hermes.',
        'Art Institute of Chicago Open Access (CC0 Public Domain)'
    );

    // Resource #6: The Old Temple — Classical Gallery (Photo, Open)
    $img6 = ensure_photo_asset($tmpDir, '06-classical-temple-gallery.jpg', 'The Old Temple — Monumental Colonnade');
    seed_asset(
        1,
        0,
        0,
        $img6,
        'The Old Temple — Monumental Colonnade (1787)',
        'Monumental architectural oil canvas by Hubert Robert (1787–1788) evoking Roman vaulted coffered ceilings and Doric colonnades.',
        'Art Institute of Chicago Open Access (CC0 Public Domain)'
    );

    // Resource #7: Illuminated Manuscript Leaf (Photo, Open)
    $img7 = ensure_photo_asset($tmpDir, '07-illuminated-manuscript-leaf.jpg', 'Illuminated Vellum Manuscript Leaf (c. 1450)');
    seed_asset(
        1,
        0,
        0,
        $img7,
        'Illuminated Vellum Manuscript Leaf (c. 1450)',
        'Fifteenth-century Italian humanist vellum folio featuring burnished gold-leaf initials with cobalt and vermilion penwork.',
        'Art Institute of Chicago Open Access (CC0 Public Domain)'
    );

    // Resource #8: Byzantine Mosaic Fragment with Grazing Camel (Photo, Open)
    $img8 = ensure_photo_asset($tmpDir, '08-byzantine-mosaic-fragment.jpg', 'Byzantine Mosaic Fragment with Grazing Camel');
    seed_asset(
        1,
        0,
        0,
        $img8,
        'Byzantine Mosaic Fragment with Grazing Camel',
        'Fifth-century eastern Mediterranean limestone and marble tesserae pavement panel depicting a belled dromedary.',
        'Art Institute of Chicago Open Access (CC0 Public Domain)'
    );
} else {
    ps_query(
        'UPDATE resource r
         INNER JOIN resource_node rn ON rn.resource = r.ref
         INNER JOIN node n ON n.ref = rn.node AND n.resource_type_field = 8
         SET r.field8 = LEFT(n.name, 200)
         WHERE r.ref > 0 AND (r.field8 IS NULL OR r.field8 = \'\')',
        []
    );
    echo "[seed] ResourceSpace already contains {$existingWithFiles} live resources (pass --reseed to replace).\n";
}

echo "\n========================================================\n";
echo "ResourceSpace 11.0 Local DAM Ready!\n";
echo "  Web UI:        {$baseUrl}\n";
echo "  Admin Login:   admin / admin123\n";
echo "  API Username:  {$apiUsername}\n";
echo "  API Key:       {$apiKey}\n";
echo "========================================================\n";

function ensure_photo_asset(string $dir, string $filename, string $fallbackLabel): string {
    $path = "{$dir}/{$filename}";
    if (file_exists($path) && filesize($path) > 1000) {
        return $path;
    }

    exec(sprintf(
        'convert -size 1200x800 xc:"#1e293b" -fill "#f8fafc" -pointsize 38 -annotate +80+400 %s %s',
        escapeshellarg($fallbackLabel),
        escapeshellarg($path)
    ));
    return $path;
}

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
    $fileSize = file_exists($destPath) ? filesize($destPath) : 0;

    ps_query(
        'UPDATE resource SET access = ?, file_extension = ?, preview_extension = ?, file_size = ?, file_modified = NOW(), no_file = 0 WHERE ref = ?',
        ['i', $accessLevel, 's', $ext, 's', 'jpg', 'i', $fileSize, 'i', $ref]
    );

    create_previews($ref, false, $ext);

    if ($ext !== 'pdf') {
        $imgInfo = @getimagesize($destPath);
        if (is_array($imgInfo) && isset($imgInfo[0], $imgInfo[1])) {
            $hasDim = (int) ps_value('SELECT COUNT(*) AS `value` FROM resource_dimensions WHERE resource = ?', ['i', $ref], 0);
            if ($hasDim === 0) {
                ps_query(
                    'INSERT INTO resource_dimensions (resource, width, height, file_size) VALUES (?, ?, ?, ?)',
                    ['i', $ref, 'i', (int) $imgInfo[0], 'i', (int) $imgInfo[1], 'i', $fileSize]
                );
            }
        }
    }

    update_field($ref, 8, $title);
    ps_query(
        'UPDATE resource SET field8 = LEFT(?, 200) WHERE ref = ?',
        ['s', $title, 'i', $ref]
    );
    update_field($ref, 18, $caption);
    if (ps_value('SELECT COUNT(*) AS `value` FROM resource_type_field WHERE ref = 10', [], 0) > 0) {
        update_field($ref, 10, $credit);
    }

    echo "[seed] Created resource #{$ref}: {$title} (ext={$ext}, stored=" . ($copied ? 'yes' : 'no') . ", access={$accessLevel})\n";
    return $ref;
}

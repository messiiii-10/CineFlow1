#!/bin/bash
# Rapor.cshtml içindeki ikinci admin nav header'ını kaldırır

FILE="$(find . -path "*/Views/Admin/Rapor.cshtml" 2>/dev/null | head -1)"

if [ -z "$FILE" ]; then
  echo "Rapor.cshtml bulunamadı. Projenin kök dizininden çalıştır."
  exit 1
fi

echo "Düzenleniyor: $FILE"

# Admin nav CSS bloğunu sil
python3 - "$FILE" << 'PYEOF'
import sys, re

path = sys.argv[1]
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# CSS bloğunu kaldır
content = re.sub(
    r'/\* Admin nav header \*/\s*\.admin-topbar \{.*?\.admin-nav__link\.active \{[^}]*\}',
    '',
    content,
    flags=re.DOTALL
)

# HTML header bloğunu kaldır
content = re.sub(
    r'<!-- NAV -->\s*<header class="admin-topbar">.*?</header>',
    '',
    content,
    flags=re.DOTALL
)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("✅ Tamamlandı! İkinci navbar kaldırıldı.")
PYEOF

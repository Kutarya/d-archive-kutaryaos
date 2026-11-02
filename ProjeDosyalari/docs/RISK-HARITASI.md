# Kutarya OS — risk haritası

5 Ekim 2026, sürüm 0.3. Olasılık ve etki puanları **tasarım tahminidir**,
istatistiksel ölçüm değildir. 1 düşük, 5 yüksek; öncelik = olasılık × etki.
1–4 düşük, 5–9 orta, 10–15 yüksek, 16–25 kritik.

| No | Risk | Olasılık | Etki | Puan | Önlem / kanıt | Kalan durum |
|---|---|---:|---:|---:|---|---|
| R1 | Gerçek dosya taşımasında kayıp | 2 | 5 | 10 | Kilitli kaynak, SHA-256, Flush; klasör ve D→C aktarımı, hata/iptal/çakışma testleri; kaynak silme sorunu açıkça bildirilir | Elle Explorer sürükleme/klasör seçimi okul PC'sinde yapılmalı |
| R2 | Küçük modelin yanlış veya zayıf cevabı | 4 | 3 | 12 | Kısa istek, JSON grameri, sınırlı araçlar; sonuç yalnız gerçek dosya işleminden | Karmaşık görev güvenilirliği garanti edilmez |
| R3 | Okul PC’sinde RAM/CPU yetersizliği | 3 | 3 | 9 | 0.6B CPU modeli, 2048 bağlam, en fazla 4 iş parçacığı; gecikme ve RAM ölçümü | Hedef PC’de ölçülmedi |
| R4 | Model indirmesinin kesilmesi / bozulması | 3 | 2 | 6 | .part + Range, boyut, GGUF ve sabit SHA; iptal/bozuk dosya testleri | İlk indirme internet gerektirir |
| R5 | AI'ın izinli alan dışına yazması | 2 | 5 | 10 | İşlem kodunda yalnız sanal Masaüstü; dış kaynak/hedef yolu reddi; host aktarım aracı ve shell yok | Uygulama OS düzeyinde bir sandbox değildir; Windows hesabı izinleri ayrıca geçerlidir |
| R6 | API anahtarının açığa çıkması / API ücreti | 2 | 4 | 8 | DPAPI, HTTPS, anahtarın kaynak/pakete alınmaması; kendi API isteğe bağlı | Kullanıcı seçtiği sağlayıcının hesabını yönetir |
| R7 | Setup yükseltme/kaldırmada veri silinmesi | 2 | 5 | 10 | Kullanıcı verisi LocalAppData’da; kullanıcı dosyalarını silen uninstall adımı yok; test kurulum/yükseltme/kaldırma | Test farklı okul PC’sinde tekrarlanmalı |
| R8 | İmzasız setup için Windows uyarısı | 3 | 2 | 6 | Paket SHA-256, kaynak arşivi; per-user yönetici gerektirmeyen kurulum | Ticari kod imzası bu teslimde yok |
| R9 | DPI / kısa ekranda düğmelerin kesilmesi | 3 | 2 | 6 | DPI ölçekleme, kaydırma, sabit alt alan; 960×640 kontrolü | 125/150% ve okul ekranı elle ölçülmedi |

## Öncelik matrisi

Satırlar etki, sütunlar olasılıktır. Boş hücre “bu sürümde listelenen risk yok”
anlamındadır; bütün risklerin yokluğu anlamına gelmez.

| Etki \\ Olasılık | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| 5 — çok yüksek | — | R1, R5, R7 | — | — | — |
| 4 — yüksek | — | R6 | — | — | — |
| 3 — orta | — | — | R3 | R2 | — |
| 2 — sınırlı | — | — | R4, R8, R9 | — | — |
| 1 — düşük | — | — | — | — | — |

## Sunum öncesi kısa kontrol

| Kontrol | Kabul |
|---|---|
| Yerel model indirildi ve panel açıldı | “Çevrimdışı hazır”; sunucu başlatılmıyor |
| Test dosyası taşındı | Yeni konumda içerik aynı; kaynak yalnız başarılı taşıma sonrası yok |
| AI yeni dosya oluşturdu | Dosyalar uygulamasında gerçek dosya görünüyor |
| Setup çalıştı ve cihaz tekrar açıldı | Cihaz adı ve dosyalar korunuyor |
| Hedef ekran ve kaynaklar | Düğmeler görünür; cevap gecikmesi kabul edilebilir |

Doğrulama ayrıntısı: KABUL.md. Risk puanları test sayısı veya model başarım
oranı olarak yorumlanmamalıdır.

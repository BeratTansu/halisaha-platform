# İl / ilçe seed verisi

`locations.json`, uygulamanın il (Province) ve ilçe (District) referans verisidir. Infrastructure'a
**EmbeddedResource** olarak gömülür ve `LocationSeeder` tarafından EF Core `UseSeeding` / `UseAsyncSeeding`
üzerinden veritabanına senkronlanır.

## Kaynak ve lisans

- Kaynak: [ubeydeozdmr/turkiye-api](https://github.com/ubeydeozdmr/turkiye-api), `datasets/2025/provinces.json`
  ve `datasets/2025/districts.json`, commit `a0ad835` (veri seti sürümü 2025, son güncelleme 2026-05-21).
  Repo'nun belirttiği il/ilçe kaynağı: TÜİK MEDAS.
- Lisans: MIT, Copyright (c) 2022-2026 Ubeyde Emir Özdemir. Kaynaktan yalnızca il/ilçe adları ve kayıt
  numaraları (`sourceId`) alındı.
- Erişim tarihi: 2026-11-05.

## Kapsam: 81 il, 973 ilçe

- 973 = 922 ilçe belediyesi (30 büyükşehirde 519 + diğer illerde 403) + büyükşehir olmayan 51 ilin **merkez
  ilçesi**. Merkez ilçelerin ayrı ilçe belediyesi yoktur ama ilçe olarak seçilebilmeleri gerekir; JSON'da
  "Merkez" adıyla yer alırlar.
- Büyükşehirlerde 519, diğer 51 ilde 454 ilçe (403 + 51 merkez).

## Id kuralları

- **İl Id = plaka kodu** (1-81).
- **İlçe Id = plaka × 1000 + sıra.** Sıra, JSON **ilk üretildiğinde** il içinde Türk alfabesine göre
  (rakamla başlayanlar önce, ör. "19 Mayıs") belirlenip dondurulmuştur.
- **Mevcut Id'ler asla yeniden hesaplanmaz.** Üretim betiği bir daha çalıştırılmaz.
- Yeni ilçe, ilinin **en büyük ilçe Id'sinin bir fazlasını** alır; kaldırılan bir Id yeniden kullanılmaz.
- İl değiştiren ilçenin Id'si değişmez, yalnızca il bağlantısı güncellenir. Bu yüzden **ilçe Id'sinden il
  türetilmez** (`Id / 1000` yapılmaz).

## sourceId

`sourceId`, kaynak veri setindeki kaydın numarasıdır ve **bizim Id'mizden farklıdır** (ör. Adana / Aladağ:
`id` 1001, `sourceId` 1757). Yalnızca JSON metadata'sıdır; veritabanında saklanmaz ve seeder tarafından okunmaz.
Kaynağın yeni bir sürümü JSON'a aktarılırken kayıtlar `sourceId` ile eşleştirilir; böylece kaynakta adı
değişen bir ilçenin bizim Id'si korunur.

## Güncelleme prosedürü

1. Kaynağın yeni sürümündeki il/ilçeleri JSON ile `sourceId` üzerinden eşleştir.
2. Eşleşen kayıtların yalnızca `name` alanını (ve gerekiyorsa ilini) güncelle; `id`'ye dokunma.
3. Yeni ilçelere yukarıdaki kurala göre yeni Id ver.
4. Kaynakta artık olmayan kaydı JSON'dan çıkarmak DB'den silmez (aşağıya bak); bilinçli karar ver.
5. `LocationDataTests` ve `LocationSeedTests` testlerini çalıştır.

## Seed davranışı

- JSON doğruluğun kaynağıdır: eksik kayıt eklenir, adı veya ili farklı olan güncellenir.
- JSON'da olmayan DB kayıtları **silinmez** (sahalar ve profiller onlara bağlı olabilir); uyarı olarak loglanır.
- Eşleştirme bizim Id'lerimizle yapılır. Mevcut bir DB'de Id'ler yanlışlıkla yeniden numaralandırılırsa
  `IX_Districts_ProvinceId_Name` ihlali (23505) ile seed başarısız olur ve geri alınır.
- Seed her `Migrate` / `MigrateAsync` / `EnsureCreated` / `dotnet ef database update` / migration bundle
  çalıştırmasında, bekleyen migration olmasa bile çalışır ve EF'in migration kilidi altındadır.
- Migration geri alındığında (ör. `database update 0`) seed yine çağrılır; tablolar yoksa hiçbir şey yapmaz.
- **Harici bir SQL script ile yapılan deploy'da seed çalışmaz.** Production deploy yöntemi seçilirken bu
  dikkate alınmalı (migration bundle ya da `Migrate` seed'i çalıştırır).

window.BENCHMARK_DATA = {
  "lastUpdate": 1781536011961,
  "repoUrl": "https://github.com/GabrielMarquezMatte/FixedWidthParser",
  "entries": {
    "Benchmark": [
      {
        "commit": {
          "author": {
            "email": "gabrielandremarquez.matte@gmail.com",
            "name": "Gabriel Matte",
            "username": "GabrielMarquezMatte"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3f09d0f2c2a343e7ecdae0370af28ec063b43653",
          "message": "Merge pull request #5 from GabrielMarquezMatte/develop",
          "timestamp": "2026-06-15T03:34:51-03:00",
          "tree_id": "81bd6a020eac5c815b5d5d02b42f28cc2201f5f2",
          "url": "https://github.com/GabrielMarquezMatte/FixedWidthParser/commit/3f09d0f2c2a343e7ecdae0370af28ec063b43653"
        },
        "date": 1781506600031,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 100)",
            "value": 5519.094629923503,
            "unit": "ns",
            "range": "± 4.0693673682151905"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 100)",
            "value": 7208.181592559815,
            "unit": "ns",
            "range": "± 9.002413993487858"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 100)",
            "value": 7354.1449920654295,
            "unit": "ns",
            "range": "± 30.957911468830147"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 100)",
            "value": 10195.667305840387,
            "unit": "ns",
            "range": "± 8.809094015099483"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 100)",
            "value": 11561.214748806424,
            "unit": "ns",
            "range": "± 8.411768334416223"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 1000)",
            "value": 55199.43717041016,
            "unit": "ns",
            "range": "± 84.83390869367742"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 1000)",
            "value": 75735.73444824219,
            "unit": "ns",
            "range": "± 84.08169844251402"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 1000)",
            "value": 78024.41856553819,
            "unit": "ns",
            "range": "± 429.86561016571056"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 1000)",
            "value": 94389.36710205078,
            "unit": "ns",
            "range": "± 271.0289031051675"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 1000)",
            "value": 117207.70734320747,
            "unit": "ns",
            "range": "± 238.79472722832665"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 100)",
            "value": 4421.105422973633,
            "unit": "ns",
            "range": "± 26.592411467898938"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 100)",
            "value": 5155.986730957031,
            "unit": "ns",
            "range": "± 14.49648357330695"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 100)",
            "value": 6472.599688720703,
            "unit": "ns",
            "range": "± 66.81414511906155"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 1000)",
            "value": 45312.295428466794,
            "unit": "ns",
            "range": "± 243.93830261931208"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 1000)",
            "value": 52103.66928100586,
            "unit": "ns",
            "range": "± 412.5076932106489"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 1000)",
            "value": 67354.10407714844,
            "unit": "ns",
            "range": "± 1330.5172405154356"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 100)",
            "value": 3979.234750535753,
            "unit": "ns",
            "range": "± 5.6934868014568645"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 100)",
            "value": 6682.81773147583,
            "unit": "ns",
            "range": "± 22.400795583284278"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 100)",
            "value": 12175.317371368408,
            "unit": "ns",
            "range": "± 7.781515866924885"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 1000)",
            "value": 34366.661312527125,
            "unit": "ns",
            "range": "± 23.405908599156913"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 1000)",
            "value": 67048.03685302734,
            "unit": "ns",
            "range": "± 303.0065831170715"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 1000)",
            "value": 124567.27663845486,
            "unit": "ns",
            "range": "± 315.1385635196309"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 100)",
            "value": 6216.993570327759,
            "unit": "ns",
            "range": "± 10.974846436884743"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 100)",
            "value": 6492.295692443848,
            "unit": "ns",
            "range": "± 77.3058796623068"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 1000)",
            "value": 61637.81333007813,
            "unit": "ns",
            "range": "± 438.5685242103346"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 1000)",
            "value": 66242.12528076171,
            "unit": "ns",
            "range": "± 185.93994499521543"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 45.246856302022934,
            "unit": "ns",
            "range": "± 0.03335804298378375"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 78.24746457073424,
            "unit": "ns",
            "range": "± 0.033947769736037255"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"John Doe   30   60000.00 \")",
            "value": 49.32069983084997,
            "unit": "ns",
            "range": "± 0.0681880716807219"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"John Doe   30   60000.00 \")",
            "value": 72.99429941177368,
            "unit": "ns",
            "range": "± 0.3165854212312011"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 100)",
            "value": 4365.386430358887,
            "unit": "ns",
            "range": "± 5.592928496024762"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 100)",
            "value": 5596.626039293077,
            "unit": "ns",
            "range": "± 11.36621593223574"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 100)",
            "value": 6448.648263719347,
            "unit": "ns",
            "range": "± 15.093629583097115"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 100)",
            "value": 8495.247968885633,
            "unit": "ns",
            "range": "± 9.32638596131864"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 100)",
            "value": 9589.615614149305,
            "unit": "ns",
            "range": "± 29.366311707111226"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 1000)",
            "value": 43668.42978922526,
            "unit": "ns",
            "range": "± 26.110526769418822"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 1000)",
            "value": 57419.83147515191,
            "unit": "ns",
            "range": "± 51.4691851778009"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 1000)",
            "value": 64919.25662231445,
            "unit": "ns",
            "range": "± 202.98888304785132"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 1000)",
            "value": 82495.14492458767,
            "unit": "ns",
            "range": "± 204.7382912665439"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 1000)",
            "value": 96233.71236165364,
            "unit": "ns",
            "range": "± 150.8587525014119"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.GeneratedReader_ReadOne",
            "value": 92.99284162786272,
            "unit": "ns",
            "range": "± 0.12738662331457318"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.ReflectionReader_ConstructAndReadOne",
            "value": 102.37718147039413,
            "unit": "ns",
            "range": "± 0.2686009252933816"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Reflection(Line: \"John Doe  30   6000000.00\")",
            "value": 48.16747270027796,
            "unit": "ns",
            "range": "± 0.15339009914236898"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Generated(Line: \"John Doe  30   6000000.00\")",
            "value": 50.17376625537872,
            "unit": "ns",
            "range": "± 0.13608322225100708"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1)",
            "value": 116.83087222576141,
            "unit": "ns",
            "range": "± 0.21067483795023026"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1)",
            "value": 117.94165648354425,
            "unit": "ns",
            "range": "± 0.0881702783504891"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1)",
            "value": 161.88298734029135,
            "unit": "ns",
            "range": "± 0.09587134753905595"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1)",
            "value": 223.55954280495644,
            "unit": "ns",
            "range": "± 0.1733602547647623"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1)",
            "value": 410.9216047392951,
            "unit": "ns",
            "range": "± 0.9783932054373988"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 100)",
            "value": 12075.249118381076,
            "unit": "ns",
            "range": "± 11.144332498361967"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 100)",
            "value": 12298.74176703559,
            "unit": "ns",
            "range": "± 7.365910616880227"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 100)",
            "value": 12475.528060913086,
            "unit": "ns",
            "range": "± 11.742478462996546"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 100)",
            "value": 13799.747029622396,
            "unit": "ns",
            "range": "± 11.002185289366258"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 100)",
            "value": 14015.293399386936,
            "unit": "ns",
            "range": "± 18.19830375479068"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1000)",
            "value": 122866.05261230469,
            "unit": "ns",
            "range": "± 92.32382531441218"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1000)",
            "value": 123698.7731689453,
            "unit": "ns",
            "range": "± 50.9891190409279"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1000)",
            "value": 124778.06530761719,
            "unit": "ns",
            "range": "± 143.50126936723987"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1000)",
            "value": 138424.26583862305,
            "unit": "ns",
            "range": "± 211.7709164620017"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1000)",
            "value": 141391.78979492188,
            "unit": "ns",
            "range": "± 290.49142570495917"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "gabrielandremarquez.matte@gmail.com",
            "name": "Gabriel Matte",
            "username": "GabrielMarquezMatte"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "365dd3b5bbf9b20da4d218018b4739c43190aed7",
          "message": "Merge pull request #6 from GabrielMarquezMatte/develop\n\nfeat: add EditorConfig, issue templates, and CI/CD workflows for impr…",
          "timestamp": "2026-06-15T11:37:07-03:00",
          "tree_id": "34784d0d490ee76fc1b75f636441f33c2d428bab",
          "url": "https://github.com/GabrielMarquezMatte/FixedWidthParser/commit/365dd3b5bbf9b20da4d218018b4739c43190aed7"
        },
        "date": 1781534577282,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 100)",
            "value": 5425.841876029968,
            "unit": "ns",
            "range": "± 3.888400836224885"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 100)",
            "value": 7053.788059997559,
            "unit": "ns",
            "range": "± 102.18779671392952"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 100)",
            "value": 7556.022347344293,
            "unit": "ns",
            "range": "± 18.548939922555036"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 100)",
            "value": 10022.458414713541,
            "unit": "ns",
            "range": "± 15.072073885188653"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 100)",
            "value": 11482.230963134765,
            "unit": "ns",
            "range": "± 31.24176989808192"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 1000)",
            "value": 53483.63941040039,
            "unit": "ns",
            "range": "± 113.20035722726765"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 1000)",
            "value": 69106.75161132813,
            "unit": "ns",
            "range": "± 964.6710220307316"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 1000)",
            "value": 72181.12121582031,
            "unit": "ns",
            "range": "± 97.2351061937393"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 1000)",
            "value": 94334.761328125,
            "unit": "ns",
            "range": "± 154.94505925827795"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 1000)",
            "value": 119041.7568359375,
            "unit": "ns",
            "range": "± 164.868625837301"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 100)",
            "value": 4685.220668792725,
            "unit": "ns",
            "range": "± 9.983026986695767"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 100)",
            "value": 5180.9491539001465,
            "unit": "ns",
            "range": "± 17.35099355970812"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 100)",
            "value": 6694.766918182373,
            "unit": "ns",
            "range": "± 14.737826802077661"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 1000)",
            "value": 46221.39039916992,
            "unit": "ns",
            "range": "± 87.75816736195155"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 1000)",
            "value": 53235.65180799696,
            "unit": "ns",
            "range": "± 127.90420185125082"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 1000)",
            "value": 66737.30627441406,
            "unit": "ns",
            "range": "± 967.4518189666979"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 100)",
            "value": 4003.375768280029,
            "unit": "ns",
            "range": "± 3.8101488138664905"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 100)",
            "value": 6170.012501186795,
            "unit": "ns",
            "range": "± 15.110492722969479"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 100)",
            "value": 13969.142810397678,
            "unit": "ns",
            "range": "± 26.293032347423523"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 1000)",
            "value": 39157.709275987414,
            "unit": "ns",
            "range": "± 21.58252894042987"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 1000)",
            "value": 63621.23541937934,
            "unit": "ns",
            "range": "± 102.98413895889934"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 1000)",
            "value": 139694.8930175781,
            "unit": "ns",
            "range": "± 1417.6260278511393"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 100)",
            "value": 5813.624204254151,
            "unit": "ns",
            "range": "± 10.234856797377695"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 100)",
            "value": 6104.551345825195,
            "unit": "ns",
            "range": "± 19.85997278389228"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 1000)",
            "value": 60658.01577148437,
            "unit": "ns",
            "range": "± 241.0133672881335"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 1000)",
            "value": 60695.40968322754,
            "unit": "ns",
            "range": "± 85.89439970422373"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 50.06170801818371,
            "unit": "ns",
            "range": "± 0.036035240442761955"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 78.53575213750203,
            "unit": "ns",
            "range": "± 0.10167648942719822"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"John Doe   30   60000.00 \")",
            "value": 53.7081088953548,
            "unit": "ns",
            "range": "± 0.4189624512745925"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"John Doe   30   60000.00 \")",
            "value": 71.85023950205908,
            "unit": "ns",
            "range": "± 0.07386529516623892"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 100)",
            "value": 4211.690056800842,
            "unit": "ns",
            "range": "± 2.1911588866116296"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 100)",
            "value": 5461.633589850531,
            "unit": "ns",
            "range": "± 7.41216785997112"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 100)",
            "value": 5875.990509033203,
            "unit": "ns",
            "range": "± 11.096575797685492"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 100)",
            "value": 8630.752735900878,
            "unit": "ns",
            "range": "± 19.649591200124306"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 100)",
            "value": 9943.843788994684,
            "unit": "ns",
            "range": "± 17.036203992288325"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 1000)",
            "value": 42677.27515258789,
            "unit": "ns",
            "range": "± 67.51200769970434"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 1000)",
            "value": 52715.70299530029,
            "unit": "ns",
            "range": "± 117.80859134388662"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 1000)",
            "value": 59263.523634168836,
            "unit": "ns",
            "range": "± 264.4498175750861"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 1000)",
            "value": 83159.64979248047,
            "unit": "ns",
            "range": "± 259.5627253864134"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 1000)",
            "value": 97574.47402615017,
            "unit": "ns",
            "range": "± 87.94560892347039"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.GeneratedReader_ReadOne",
            "value": 95.42233635319604,
            "unit": "ns",
            "range": "± 0.07030251751392358"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.ReflectionReader_ConstructAndReadOne",
            "value": 127.53021907806396,
            "unit": "ns",
            "range": "± 0.12605821525213978"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Reflection(Line: \"John Doe  30   6000000.00\")",
            "value": 48.7518342534701,
            "unit": "ns",
            "range": "± 0.11623461014429752"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Generated(Line: \"John Doe  30   6000000.00\")",
            "value": 50.48127063512802,
            "unit": "ns",
            "range": "± 0.12895384871622054"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1)",
            "value": 119.48545289039612,
            "unit": "ns",
            "range": "± 0.08844077385523223"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1)",
            "value": 119.80607197284698,
            "unit": "ns",
            "range": "± 0.3527889248750615"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1)",
            "value": 177.39463464419046,
            "unit": "ns",
            "range": "± 0.41607966961492815"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1)",
            "value": 273.2026673316956,
            "unit": "ns",
            "range": "± 9.494331851699107"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1)",
            "value": 552.4711515426636,
            "unit": "ns",
            "range": "± 10.827537536884286"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 100)",
            "value": 12491.831337822809,
            "unit": "ns",
            "range": "± 64.78082322780661"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 100)",
            "value": 12780.007148742676,
            "unit": "ns",
            "range": "± 8.278267180062679"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 100)",
            "value": 13397.39235534668,
            "unit": "ns",
            "range": "± 31.758984933888286"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 100)",
            "value": 14231.267956203885,
            "unit": "ns",
            "range": "± 31.60387238501361"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 100)",
            "value": 14631.192993164062,
            "unit": "ns",
            "range": "± 56.24682282322688"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1000)",
            "value": 124514.32162475586,
            "unit": "ns",
            "range": "± 164.75539750829762"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1000)",
            "value": 125095.48977661133,
            "unit": "ns",
            "range": "± 99.31161107788368"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1000)",
            "value": 127313.84318033855,
            "unit": "ns",
            "range": "± 254.11315441018476"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1000)",
            "value": 142526.91793823242,
            "unit": "ns",
            "range": "± 242.06136117625"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1000)",
            "value": 149666.57497829862,
            "unit": "ns",
            "range": "± 318.7569018572843"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "gabrielandremarquez.matte@gmail.com",
            "name": "Gabriel Matte",
            "username": "GabrielMarquezMatte"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d391b84d16b6b48662e384c9eb7c0237ed0fbd8e",
          "message": "Merge pull request #15 from GabrielMarquezMatte/develop\n\nUpdate multiple dependencies including GitHub Actions and analyzers",
          "timestamp": "2026-06-15T12:00:48-03:00",
          "tree_id": "fc848cfbd1f8540793ce98afcd4a6ec3b9883cfe",
          "url": "https://github.com/GabrielMarquezMatte/FixedWidthParser/commit/d391b84d16b6b48662e384c9eb7c0237ed0fbd8e"
        },
        "date": 1781536011596,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 100)",
            "value": 5347.798184967041,
            "unit": "ns",
            "range": "± 35.968315137316914"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 100)",
            "value": 7298.312022738986,
            "unit": "ns",
            "range": "± 21.63567732515251"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 100)",
            "value": 7884.710257636176,
            "unit": "ns",
            "range": "± 149.72647602432153"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 100)",
            "value": 10955.211198594836,
            "unit": "ns",
            "range": "± 14.139484408978044"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 100)",
            "value": 12615.298897637262,
            "unit": "ns",
            "range": "± 16.614930822422515"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 1000)",
            "value": 52626.268566894534,
            "unit": "ns",
            "range": "± 239.08854005940842"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 1000)",
            "value": 68990.21445041233,
            "unit": "ns",
            "range": "± 512.623538603507"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 1000)",
            "value": 75972.46690673828,
            "unit": "ns",
            "range": "± 173.48285102072222"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 1000)",
            "value": 106964.88774278428,
            "unit": "ns",
            "range": "± 130.81523737325602"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 1000)",
            "value": 122125.72093505859,
            "unit": "ns",
            "range": "± 269.92415346281314"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 100)",
            "value": 4770.683671569825,
            "unit": "ns",
            "range": "± 25.81506971358842"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 100)",
            "value": 5726.754493713379,
            "unit": "ns",
            "range": "± 36.605170349994744"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 100)",
            "value": 6899.515433502197,
            "unit": "ns",
            "range": "± 105.36510210627739"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 1000)",
            "value": 46710.13381347656,
            "unit": "ns",
            "range": "± 514.5440888766333"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 1000)",
            "value": 53203.64205932617,
            "unit": "ns",
            "range": "± 272.3675994392623"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 1000)",
            "value": 66431.19091796875,
            "unit": "ns",
            "range": "± 1114.4934596820876"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 100)",
            "value": 3488.5232334136963,
            "unit": "ns",
            "range": "± 2.26137083481197"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 100)",
            "value": 5940.259615325927,
            "unit": "ns",
            "range": "± 31.19682324293294"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 100)",
            "value": 13016.224200439454,
            "unit": "ns",
            "range": "± 19.203693186816235"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 1000)",
            "value": 37181.69192504883,
            "unit": "ns",
            "range": "± 24.091906227072613"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 1000)",
            "value": 62987.361645507815,
            "unit": "ns",
            "range": "± 219.6477174369513"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 1000)",
            "value": 139578.32215711806,
            "unit": "ns",
            "range": "± 143.74628485544116"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 100)",
            "value": 5855.145952606201,
            "unit": "ns",
            "range": "± 25.678112434826676"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 100)",
            "value": 6514.408489990235,
            "unit": "ns",
            "range": "± 65.7898704409275"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 1000)",
            "value": 58176.473758273656,
            "unit": "ns",
            "range": "± 153.7669325796211"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 1000)",
            "value": 61681.29055175781,
            "unit": "ns",
            "range": "± 355.8239061674809"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 50.18219541178809,
            "unit": "ns",
            "range": "± 0.17453378609600967"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 78.26263874769211,
            "unit": "ns",
            "range": "± 0.1719156488800313"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"John Doe   30   60000.00 \")",
            "value": 49.04792900217904,
            "unit": "ns",
            "range": "± 0.14898928355542376"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"John Doe   30   60000.00 \")",
            "value": 70.63415355682373,
            "unit": "ns",
            "range": "± 0.11340385213395955"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 100)",
            "value": 4113.786697387695,
            "unit": "ns",
            "range": "± 15.30394168450195"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 100)",
            "value": 5755.197163899739,
            "unit": "ns",
            "range": "± 24.891805921579056"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 100)",
            "value": 6291.77945098877,
            "unit": "ns",
            "range": "± 48.48169114309144"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 100)",
            "value": 8955.486152648926,
            "unit": "ns",
            "range": "± 56.3556965016411"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 100)",
            "value": 10318.797888861762,
            "unit": "ns",
            "range": "± 23.381547595067893"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 1000)",
            "value": 42069.69600219726,
            "unit": "ns",
            "range": "± 35.69098477216085"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 1000)",
            "value": 56507.002014160156,
            "unit": "ns",
            "range": "± 173.15472923378604"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 1000)",
            "value": 62926.10007324219,
            "unit": "ns",
            "range": "± 581.0230442830814"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 1000)",
            "value": 88084.68166503907,
            "unit": "ns",
            "range": "± 476.3175851102067"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 1000)",
            "value": 109284.24942626953,
            "unit": "ns",
            "range": "± 212.85491773857225"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.GeneratedReader_ReadOne",
            "value": 100.67512120803197,
            "unit": "ns",
            "range": "± 0.4702488578350342"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.ReflectionReader_ConstructAndReadOne",
            "value": 109.46660368972354,
            "unit": "ns",
            "range": "± 0.16986173191957515"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Reflection(Line: \"John Doe  30   6000000.00\")",
            "value": 48.38991579082277,
            "unit": "ns",
            "range": "± 0.03909902807172146"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Generated(Line: \"John Doe  30   6000000.00\")",
            "value": 49.99931836790509,
            "unit": "ns",
            "range": "± 0.052260289714595475"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1)",
            "value": 119.56240563922458,
            "unit": "ns",
            "range": "± 0.269329966444491"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1)",
            "value": 119.59516954421997,
            "unit": "ns",
            "range": "± 0.15684508525984137"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1)",
            "value": 159.30774328112602,
            "unit": "ns",
            "range": "± 0.2830149660054458"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1)",
            "value": 238.7091513633728,
            "unit": "ns",
            "range": "± 3.335109688179908"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1)",
            "value": 442.2631782531738,
            "unit": "ns",
            "range": "± 4.327702437330243"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 100)",
            "value": 12001.589601516724,
            "unit": "ns",
            "range": "± 5.740809302210033"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 100)",
            "value": 12330.139575195313,
            "unit": "ns",
            "range": "± 4.0502440863121105"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 100)",
            "value": 12598.445651584201,
            "unit": "ns",
            "range": "± 17.14940476863752"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 100)",
            "value": 14062.878823174371,
            "unit": "ns",
            "range": "± 45.74809769249608"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 100)",
            "value": 14153.261967468261,
            "unit": "ns",
            "range": "± 85.31361219369084"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1000)",
            "value": 120064.83129882812,
            "unit": "ns",
            "range": "± 244.78983054381322"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1000)",
            "value": 121903.23205566406,
            "unit": "ns",
            "range": "± 1253.4305829345155"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1000)",
            "value": 125769.31143188477,
            "unit": "ns",
            "range": "± 90.98748276854836"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1000)",
            "value": 142911.63088650175,
            "unit": "ns",
            "range": "± 88.05087499700647"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1000)",
            "value": 143205.4670952691,
            "unit": "ns",
            "range": "± 122.38588920246971"
          }
        ]
      }
    ]
  }
}
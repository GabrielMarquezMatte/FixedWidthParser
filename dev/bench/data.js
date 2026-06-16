window.BENCHMARK_DATA = {
  "lastUpdate": 1781650761434,
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
          "id": "aea83784886b04735056dc079fa9d9446268ab24",
          "message": "Merge pull request #16 from GabrielMarquezMatte/develop\n\nImplementing System.IO.Pipeline integration and fixing non ASCII decimal separator bugs for utf-8 parser",
          "timestamp": "2026-06-15T17:20:00-03:00",
          "tree_id": "5a094f1f8b5fc3bf3d066d0a06b378fd53cdf2cb",
          "url": "https://github.com/GabrielMarquezMatte/FixedWidthParser/commit/aea83784886b04735056dc079fa9d9446268ab24"
        },
        "date": 1781555140267,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 100)",
            "value": 4411.214863247342,
            "unit": "ns",
            "range": "± 10.588687734226498"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 100)",
            "value": 5665.8549530029295,
            "unit": "ns",
            "range": "± 46.878142972596045"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 100)",
            "value": 5802.418286132813,
            "unit": "ns",
            "range": "± 14.186476814957366"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 100)",
            "value": 8028.123245239258,
            "unit": "ns",
            "range": "± 15.644399576460483"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 100)",
            "value": 9201.925340652466,
            "unit": "ns",
            "range": "± 11.176998251214627"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 1000)",
            "value": 43826.08808390299,
            "unit": "ns",
            "range": "± 157.49859949549912"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 1000)",
            "value": 57780.88073730469,
            "unit": "ns",
            "range": "± 705.831314991516"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 1000)",
            "value": 63129.88042534722,
            "unit": "ns",
            "range": "± 101.5076948648712"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 1000)",
            "value": 72440.08338758681,
            "unit": "ns",
            "range": "± 121.30426442844094"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 1000)",
            "value": 91136.17414008247,
            "unit": "ns",
            "range": "± 134.2468697375125"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 100)",
            "value": 4958.5248616536455,
            "unit": "ns",
            "range": "± 17.8532311858266"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 100)",
            "value": 5350.471490859985,
            "unit": "ns",
            "range": "± 8.259272342834722"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 100)",
            "value": 7231.437512969971,
            "unit": "ns",
            "range": "± 17.349238374828545"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 1000)",
            "value": 44598.22359890408,
            "unit": "ns",
            "range": "± 102.6018939623561"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 1000)",
            "value": 57192.18591647678,
            "unit": "ns",
            "range": "± 73.03830237977658"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 1000)",
            "value": 66504.94956054687,
            "unit": "ns",
            "range": "± 388.7828411414201"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 100)",
            "value": 4067.2998809814453,
            "unit": "ns",
            "range": "± 1.5392024013908523"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 100)",
            "value": 6363.397844696045,
            "unit": "ns",
            "range": "± 17.076066372062208"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 100)",
            "value": 13625.549240112305,
            "unit": "ns",
            "range": "± 20.727957498433312"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 1000)",
            "value": 39611.70616149902,
            "unit": "ns",
            "range": "± 24.12502820872542"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 1000)",
            "value": 58799.79371948242,
            "unit": "ns",
            "range": "± 423.0448187038241"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 1000)",
            "value": 136540.98259277345,
            "unit": "ns",
            "range": "± 93.87476611253231"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 100)",
            "value": 5883.154500579834,
            "unit": "ns",
            "range": "± 10.875505218101063"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 100)",
            "value": 6545.710189819336,
            "unit": "ns",
            "range": "± 32.63098283335327"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 1000)",
            "value": 62383.121066623266,
            "unit": "ns",
            "range": "± 160.56044685940628"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 1000)",
            "value": 64715.31376647949,
            "unit": "ns",
            "range": "± 56.43229602394587"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 48.94736687839031,
            "unit": "ns",
            "range": "± 0.08565212422880321"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 76.76296427249909,
            "unit": "ns",
            "range": "± 0.07766048563451242"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"John Doe   30   60000.00 \")",
            "value": 47.63991749882698,
            "unit": "ns",
            "range": "± 0.17435225323778197"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"John Doe   30   60000.00 \")",
            "value": 73.821502161026,
            "unit": "ns",
            "range": "± 0.10683512571601585"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 100)",
            "value": 7911.921620686849,
            "unit": "ns",
            "range": "± 14.977071607873363"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 100)",
            "value": 19463.536392211914,
            "unit": "ns",
            "range": "± 31.792663041941253"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 100)",
            "value": 64740.38136291504,
            "unit": "ns",
            "range": "± 53.18511665434668"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 1000)",
            "value": 81109.8951687283,
            "unit": "ns",
            "range": "± 148.21837264703484"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 1000)",
            "value": 190600.64336480034,
            "unit": "ns",
            "range": "± 886.2377227290256"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 1000)",
            "value": 636074.5021484375,
            "unit": "ns",
            "range": "± 1721.8405066634703"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 100)",
            "value": 3370.99280128479,
            "unit": "ns",
            "range": "± 29.55404430602478"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 100)",
            "value": 4284.491155836317,
            "unit": "ns",
            "range": "± 22.222393753039366"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 100)",
            "value": 5048.259564971924,
            "unit": "ns",
            "range": "± 49.00849285555165"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 100)",
            "value": 6683.36803012424,
            "unit": "ns",
            "range": "± 15.783464903379057"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 100)",
            "value": 7959.226763916015,
            "unit": "ns",
            "range": "± 21.107929573065938"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 1000)",
            "value": 33111.40242919922,
            "unit": "ns",
            "range": "± 33.94422377869856"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 1000)",
            "value": 42806.12214152018,
            "unit": "ns",
            "range": "± 60.47947478346094"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 1000)",
            "value": 48292.98090209961,
            "unit": "ns",
            "range": "± 310.32976291011954"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 1000)",
            "value": 63736.17071533203,
            "unit": "ns",
            "range": "± 140.8107892526471"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 1000)",
            "value": 78551.80807495117,
            "unit": "ns",
            "range": "± 142.51951709838656"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.GeneratedReader_ReadOne",
            "value": 70.5707610183292,
            "unit": "ns",
            "range": "± 0.07894602258175634"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.ReflectionReader_ConstructAndReadOne",
            "value": 95.73131635453966,
            "unit": "ns",
            "range": "± 0.47467071766364455"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Reflection(Line: \"John Doe  30   6000000.00\")",
            "value": 47.618158439795174,
            "unit": "ns",
            "range": "± 0.05769746888009758"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Generated(Line: \"John Doe  30   6000000.00\")",
            "value": 50.8846430712276,
            "unit": "ns",
            "range": "± 0.021786779007244298"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1)",
            "value": 116.98001461558871,
            "unit": "ns",
            "range": "± 0.2444847771464982"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1)",
            "value": 118.16097241640091,
            "unit": "ns",
            "range": "± 0.11164065259377494"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1)",
            "value": 170.0936080932617,
            "unit": "ns",
            "range": "± 0.3509421549856173"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1)",
            "value": 292.03455529212954,
            "unit": "ns",
            "range": "± 8.957303792369375"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1)",
            "value": 564.6518692016601,
            "unit": "ns",
            "range": "± 34.60685111435814"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 100)",
            "value": 12407.875523376464,
            "unit": "ns",
            "range": "± 60.75426366725969"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 100)",
            "value": 12861.50501675076,
            "unit": "ns",
            "range": "± 27.253703191983636"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 100)",
            "value": 13078.278361511231,
            "unit": "ns",
            "range": "± 18.030464767221748"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 100)",
            "value": 14516.310087415906,
            "unit": "ns",
            "range": "± 16.001923644998683"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 100)",
            "value": 15382.090003967285,
            "unit": "ns",
            "range": "± 12.30892532385968"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1000)",
            "value": 124410.44808959961,
            "unit": "ns",
            "range": "± 107.51955927112313"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1000)",
            "value": 124628.62557983398,
            "unit": "ns",
            "range": "± 169.7339405231761"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1000)",
            "value": 126073.9507446289,
            "unit": "ns",
            "range": "± 158.01204325121145"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1000)",
            "value": 145892.85150824653,
            "unit": "ns",
            "range": "± 263.8759855615193"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1000)",
            "value": 146919.61892361112,
            "unit": "ns",
            "range": "± 324.7159584941929"
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
          "id": "962a75a93904ba390bc4daee252eb0131bcb90f9",
          "message": "Merge pull request #17 from GabrielMarquezMatte/develop\n\nFixing code coverage",
          "timestamp": "2026-06-15T17:43:20-03:00",
          "tree_id": "e56aecdf1fea793bb6cc0cc2a42c39a4ab44f5ac",
          "url": "https://github.com/GabrielMarquezMatte/FixedWidthParser/commit/962a75a93904ba390bc4daee252eb0131bcb90f9"
        },
        "date": 1781556554596,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 100)",
            "value": 5562.0609878540035,
            "unit": "ns",
            "range": "± 8.39782879747717"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 100)",
            "value": 7223.735111999512,
            "unit": "ns",
            "range": "± 143.16727944943102"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 100)",
            "value": 7534.183269924588,
            "unit": "ns",
            "range": "± 24.001858624180628"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 100)",
            "value": 10090.263676452636,
            "unit": "ns",
            "range": "± 40.03423147496413"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 100)",
            "value": 13201.742464065552,
            "unit": "ns",
            "range": "± 11.354066634407358"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 1000)",
            "value": 54246.79896714952,
            "unit": "ns",
            "range": "± 125.35940427647783"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 1000)",
            "value": 69006.64252929688,
            "unit": "ns",
            "range": "± 390.5309371418832"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 1000)",
            "value": 71897.13894314236,
            "unit": "ns",
            "range": "± 97.61099641054538"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 1000)",
            "value": 96028.37937011718,
            "unit": "ns",
            "range": "± 149.88801126807755"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 1000)",
            "value": 115348.42185058593,
            "unit": "ns",
            "range": "± 477.5719506511322"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 100)",
            "value": 4773.905932426453,
            "unit": "ns",
            "range": "± 24.39173483819744"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 100)",
            "value": 5495.597836494446,
            "unit": "ns",
            "range": "± 11.424686739456778"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 100)",
            "value": 6516.476831817627,
            "unit": "ns",
            "range": "± 141.30675230675976"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 1000)",
            "value": 46543.438049316406,
            "unit": "ns",
            "range": "± 428.24881142156755"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 1000)",
            "value": 51913.21788330078,
            "unit": "ns",
            "range": "± 342.9461621846654"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 1000)",
            "value": 67766.05257161458,
            "unit": "ns",
            "range": "± 511.0242378363247"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 100)",
            "value": 4012.0700393676757,
            "unit": "ns",
            "range": "± 4.468235188231691"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 100)",
            "value": 6443.20549697876,
            "unit": "ns",
            "range": "± 46.54698197961105"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 100)",
            "value": 13405.432233810425,
            "unit": "ns",
            "range": "± 14.519734485068867"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 1000)",
            "value": 39273.363021850586,
            "unit": "ns",
            "range": "± 41.48606303543964"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 1000)",
            "value": 58042.96918402778,
            "unit": "ns",
            "range": "± 183.52945409638892"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 1000)",
            "value": 131505.1037902832,
            "unit": "ns",
            "range": "± 252.78486456242902"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 100)",
            "value": 5941.134525299072,
            "unit": "ns",
            "range": "± 61.07429874542897"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 100)",
            "value": 6146.82396774292,
            "unit": "ns",
            "range": "± 130.13397136681476"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 1000)",
            "value": 56109.9339477539,
            "unit": "ns",
            "range": "± 791.4297498320803"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 1000)",
            "value": 65056.40665283203,
            "unit": "ns",
            "range": "± 308.4951758429571"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 49.65246541500092,
            "unit": "ns",
            "range": "± 0.184924524413554"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 79.37334977090359,
            "unit": "ns",
            "range": "± 0.12092158522023089"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"John Doe   30   60000.00 \")",
            "value": 49.32296870946884,
            "unit": "ns",
            "range": "± 0.18135401627193817"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"John Doe   30   60000.00 \")",
            "value": 70.84369205766254,
            "unit": "ns",
            "range": "± 0.037167628534582324"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 100)",
            "value": 8032.8630294799805,
            "unit": "ns",
            "range": "± 58.21222379615047"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 100)",
            "value": 20457.97564315796,
            "unit": "ns",
            "range": "± 42.969778319567745"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 100)",
            "value": 58673.90385742187,
            "unit": "ns",
            "range": "± 75.57259404194302"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 1000)",
            "value": 79275.53302680122,
            "unit": "ns",
            "range": "± 285.5463713143724"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 1000)",
            "value": 200547.33056640625,
            "unit": "ns",
            "range": "± 472.9942818107112"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 1000)",
            "value": 589950.5240885416,
            "unit": "ns",
            "range": "± 2038.4066680579458"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 100)",
            "value": 4545.497942352295,
            "unit": "ns",
            "range": "± 15.435449623743258"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 100)",
            "value": 5656.8790481567385,
            "unit": "ns",
            "range": "± 39.21707117720469"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 100)",
            "value": 6233.857901679145,
            "unit": "ns",
            "range": "± 42.00621559891319"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 100)",
            "value": 8642.184280395508,
            "unit": "ns",
            "range": "± 5.848000464445473"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 100)",
            "value": 9518.854804144965,
            "unit": "ns",
            "range": "± 22.210664390898753"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 1000)",
            "value": 42840.5234781901,
            "unit": "ns",
            "range": "± 281.0187258216399"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 1000)",
            "value": 56507.501428222655,
            "unit": "ns",
            "range": "± 153.3376509276101"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 1000)",
            "value": 63770.66303710938,
            "unit": "ns",
            "range": "± 278.50354862616393"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 1000)",
            "value": 84668.59772949219,
            "unit": "ns",
            "range": "± 178.86378633008775"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 1000)",
            "value": 103501.8547498915,
            "unit": "ns",
            "range": "± 204.02979853289543"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.GeneratedReader_ReadOne",
            "value": 90.20048387348652,
            "unit": "ns",
            "range": "± 0.558592268741745"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.ReflectionReader_ConstructAndReadOne",
            "value": 120.32180540561676,
            "unit": "ns",
            "range": "± 0.49844205580181217"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Reflection(Line: \"John Doe  30   6000000.00\")",
            "value": 48.70998223291503,
            "unit": "ns",
            "range": "± 0.01715854341284813"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Generated(Line: \"John Doe  30   6000000.00\")",
            "value": 49.572677436802124,
            "unit": "ns",
            "range": "± 0.05261130848574942"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1)",
            "value": 116.69971309767828,
            "unit": "ns",
            "range": "± 0.08741480484623765"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1)",
            "value": 117.44601713286505,
            "unit": "ns",
            "range": "± 0.09391694299603155"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1)",
            "value": 165.1335532426834,
            "unit": "ns",
            "range": "± 0.27831876903127994"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1)",
            "value": 326.5137848854065,
            "unit": "ns",
            "range": "± 6.080404609281732"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1)",
            "value": 654.0132206916809,
            "unit": "ns",
            "range": "± 10.478705605547452"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 100)",
            "value": 12196.968217468262,
            "unit": "ns",
            "range": "± 8.777760879480917"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 100)",
            "value": 12313.826563517252,
            "unit": "ns",
            "range": "± 11.863500183290666"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 100)",
            "value": 13088.279713948568,
            "unit": "ns",
            "range": "± 16.68620443017555"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 100)",
            "value": 13598.78662261963,
            "unit": "ns",
            "range": "± 19.371185220746423"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 100)",
            "value": 14386.227728949652,
            "unit": "ns",
            "range": "± 13.596592328065617"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1000)",
            "value": 122587.12540690105,
            "unit": "ns",
            "range": "± 775.2301537502218"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1000)",
            "value": 122679.38218858506,
            "unit": "ns",
            "range": "± 93.31476530815831"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1000)",
            "value": 125222.53900146484,
            "unit": "ns",
            "range": "± 64.19977230387282"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1000)",
            "value": 134888.2787109375,
            "unit": "ns",
            "range": "± 72.24835938675777"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1000)",
            "value": 135999.63876953124,
            "unit": "ns",
            "range": "± 313.1951879506281"
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
          "id": "1f6cf9e168521c5e7cb41c59522949bfedc91252",
          "message": "Merge pull request #18 from GabrielMarquezMatte/develop\n\nUpdate maintainer email address in security policy",
          "timestamp": "2026-06-15T18:10:59-03:00",
          "tree_id": "4dc8a83de6b5e0fb6429f51705e63ad10b00b4f1",
          "url": "https://github.com/GabrielMarquezMatte/FixedWidthParser/commit/1f6cf9e168521c5e7cb41c59522949bfedc91252"
        },
        "date": 1781558204631,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 100)",
            "value": 5207.694100952149,
            "unit": "ns",
            "range": "± 17.588123273206488"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 100)",
            "value": 7317.765128326416,
            "unit": "ns",
            "range": "± 132.11253131475428"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 100)",
            "value": 7376.819197845459,
            "unit": "ns",
            "range": "± 16.422453585849166"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 100)",
            "value": 10805.802909003363,
            "unit": "ns",
            "range": "± 21.342822690314485"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 100)",
            "value": 12461.918989181519,
            "unit": "ns",
            "range": "± 7.551436922751747"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 1000)",
            "value": 51926.524041069875,
            "unit": "ns",
            "range": "± 110.56194660496573"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 1000)",
            "value": 71617.84709472656,
            "unit": "ns",
            "range": "± 769.7791943442548"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 1000)",
            "value": 72898.72432861329,
            "unit": "ns",
            "range": "± 172.23108016986842"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 1000)",
            "value": 101688.52989366319,
            "unit": "ns",
            "range": "± 117.03110875489857"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 1000)",
            "value": 121826.38495551216,
            "unit": "ns",
            "range": "± 334.31142628857947"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 100)",
            "value": 4953.941871643066,
            "unit": "ns",
            "range": "± 14.436406999371552"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 100)",
            "value": 5463.698341369629,
            "unit": "ns",
            "range": "± 27.703835957078486"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 100)",
            "value": 7076.1991912841795,
            "unit": "ns",
            "range": "± 31.924219845804977"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 1000)",
            "value": 43679.090488009984,
            "unit": "ns",
            "range": "± 164.97760142464682"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 1000)",
            "value": 52815.46448364258,
            "unit": "ns",
            "range": "± 256.3830722944752"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 1000)",
            "value": 67473.14096679687,
            "unit": "ns",
            "range": "± 282.47500117801025"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 100)",
            "value": 4126.905157470703,
            "unit": "ns",
            "range": "± 3.830208825439861"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 100)",
            "value": 6417.723342132568,
            "unit": "ns",
            "range": "± 27.651765027014115"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 100)",
            "value": 13700.605427551269,
            "unit": "ns",
            "range": "± 36.79392444233168"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 1000)",
            "value": 37709.130879720055,
            "unit": "ns",
            "range": "± 22.278120070113417"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 1000)",
            "value": 64701.872314453125,
            "unit": "ns",
            "range": "± 281.21424410689787"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 1000)",
            "value": 138974.396742079,
            "unit": "ns",
            "range": "± 129.8093365074126"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 100)",
            "value": 6350.893547821045,
            "unit": "ns",
            "range": "± 94.43109277749907"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 100)",
            "value": 6456.357223510742,
            "unit": "ns",
            "range": "± 9.143553448293837"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 1000)",
            "value": 60585.48945922851,
            "unit": "ns",
            "range": "± 230.91175516789278"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 1000)",
            "value": 67688.29892306858,
            "unit": "ns",
            "range": "± 86.77763610168076"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 45.789446141984726,
            "unit": "ns",
            "range": "± 0.2507758206856039"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 76.7687137722969,
            "unit": "ns",
            "range": "± 0.09310042803303102"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"John Doe   30   60000.00 \")",
            "value": 49.98769048187468,
            "unit": "ns",
            "range": "± 0.07832520299745917"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"John Doe   30   60000.00 \")",
            "value": 72.16996757851706,
            "unit": "ns",
            "range": "± 0.08114112716553423"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 100)",
            "value": 8927.807321548462,
            "unit": "ns",
            "range": "± 6.6483934503803175"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 100)",
            "value": 20352.32209269206,
            "unit": "ns",
            "range": "± 31.107275450671676"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 100)",
            "value": 62652.55445692274,
            "unit": "ns",
            "range": "± 133.09117269407335"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 1000)",
            "value": 76641.50377197265,
            "unit": "ns",
            "range": "± 129.34555928461072"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 1000)",
            "value": 196566.2388780382,
            "unit": "ns",
            "range": "± 355.5966865329861"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 1000)",
            "value": 626013.6006835938,
            "unit": "ns",
            "range": "± 1929.8285466442605"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 100)",
            "value": 3991.89700656467,
            "unit": "ns",
            "range": "± 5.318314080512899"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 100)",
            "value": 6481.686827087402,
            "unit": "ns",
            "range": "± 49.93262176464945"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 100)",
            "value": 6830.830691019694,
            "unit": "ns",
            "range": "± 9.825532379696885"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 100)",
            "value": 9010.961406707764,
            "unit": "ns",
            "range": "± 46.322266937044446"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 100)",
            "value": 11515.416630554198,
            "unit": "ns",
            "range": "± 33.56715762451006"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 1000)",
            "value": 40259.251934814456,
            "unit": "ns",
            "range": "± 212.75382918979005"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 1000)",
            "value": 54829.00439114041,
            "unit": "ns",
            "range": "± 69.70432555860883"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 1000)",
            "value": 62141.71684570312,
            "unit": "ns",
            "range": "± 582.5405057426261"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 1000)",
            "value": 85786.43555908203,
            "unit": "ns",
            "range": "± 1045.7123019704213"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 1000)",
            "value": 107366.1276611328,
            "unit": "ns",
            "range": "± 163.0829214317611"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.GeneratedReader_ReadOne",
            "value": 96.87294818162918,
            "unit": "ns",
            "range": "± 0.28469869502635076"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.ReflectionReader_ConstructAndReadOne",
            "value": 128.32074451446533,
            "unit": "ns",
            "range": "± 0.5913952963087055"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Reflection(Line: \"John Doe  30   6000000.00\")",
            "value": 47.96586352586746,
            "unit": "ns",
            "range": "± 0.03670690207215476"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Generated(Line: \"John Doe  30   6000000.00\")",
            "value": 50.49005459414588,
            "unit": "ns",
            "range": "± 0.043971948495401626"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1)",
            "value": 118.46983733442094,
            "unit": "ns",
            "range": "± 0.11957784382696611"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1)",
            "value": 122.75925585627556,
            "unit": "ns",
            "range": "± 0.15187874058935155"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1)",
            "value": 160.37058573961258,
            "unit": "ns",
            "range": "± 0.16914543821653524"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1)",
            "value": 227.64670084582434,
            "unit": "ns",
            "range": "± 2.433703588219609"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1)",
            "value": 429.29029824998645,
            "unit": "ns",
            "range": "± 4.422649736530816"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 100)",
            "value": 12075.917426215277,
            "unit": "ns",
            "range": "± 73.80248741622748"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 100)",
            "value": 12330.11353111267,
            "unit": "ns",
            "range": "± 7.528153605730764"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 100)",
            "value": 12995.669198989868,
            "unit": "ns",
            "range": "± 25.973557436033865"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 100)",
            "value": 14053.526531219482,
            "unit": "ns",
            "range": "± 62.268635415199824"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 100)",
            "value": 14238.637453079224,
            "unit": "ns",
            "range": "± 48.03455450743848"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1000)",
            "value": 122977.64343261719,
            "unit": "ns",
            "range": "± 121.05904510361434"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1000)",
            "value": 123957.05604383681,
            "unit": "ns",
            "range": "± 142.72353325244572"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1000)",
            "value": 137761.21280924478,
            "unit": "ns",
            "range": "± 239.77857840019286"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1000)",
            "value": 138652.10394965278,
            "unit": "ns",
            "range": "± 633.1487413170643"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1000)",
            "value": 139443.8931749132,
            "unit": "ns",
            "range": "± 270.99732576712523"
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
          "id": "102c1df0c284e094e11d2122d85fa47c852ebd7f",
          "message": "Merge pull request #19 from GabrielMarquezMatte/develop",
          "timestamp": "2026-06-16T19:53:15-03:00",
          "tree_id": "ed9fa5bd35bce3832e7ea868e8f3926eb6eec119",
          "url": "https://github.com/GabrielMarquezMatte/FixedWidthParser/commit/102c1df0c284e094e11d2122d85fa47c852ebd7f"
        },
        "date": 1781650760486,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 100)",
            "value": 5466.9814195632935,
            "unit": "ns",
            "range": "± 15.036113284318592"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 100)",
            "value": 7649.677664862738,
            "unit": "ns",
            "range": "± 12.983477455424302"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 100)",
            "value": 7782.25530327691,
            "unit": "ns",
            "range": "± 71.91013080029069"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 100)",
            "value": 9820.053512573242,
            "unit": "ns",
            "range": "± 29.896812070229643"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 100)",
            "value": 12468.676723480225,
            "unit": "ns",
            "range": "± 24.672079266409888"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync(Count: 1000)",
            "value": 55525.250732421875,
            "unit": "ns",
            "range": "± 491.16449603363355"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.Naive_ReadLineAsync(Count: 1000)",
            "value": 73311.83930664063,
            "unit": "ns",
            "range": "± 680.8944873743652"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync(Count: 1000)",
            "value": 75329.89298502605,
            "unit": "ns",
            "range": "± 177.78516516627735"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.GeneratedReader_ReadAsync_Pooled(Count: 1000)",
            "value": 94237.11402723525,
            "unit": "ns",
            "range": "± 222.32129744873154"
          },
          {
            "name": "Benchmarks.Perf.AsyncReaderBenchmarks.SpanReader_ReadAsync_Pooled(Count: 1000)",
            "value": 119979.8990749783,
            "unit": "ns",
            "range": "± 55.15468798250458"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 100)",
            "value": 4731.313004493713,
            "unit": "ns",
            "range": "± 9.901749505980694"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 100)",
            "value": 5133.178148651123,
            "unit": "ns",
            "range": "± 13.363386159990839"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 100)",
            "value": 7225.336807250977,
            "unit": "ns",
            "range": "± 33.580861267821085"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse(Count: 1000)",
            "value": 47212.76251898872,
            "unit": "ns",
            "range": "± 384.80537630349124"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.ByteParser_Parse(Count: 1000)",
            "value": 50798.49869384766,
            "unit": "ns",
            "range": "± 213.67931194086376"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderBenchmarks.CharParser_Parse_AfterUtf8Decode(Count: 1000)",
            "value": 72274.1096435547,
            "unit": "ns",
            "range": "± 501.521870604851"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 100)",
            "value": 3467.3851263258193,
            "unit": "ns",
            "range": "± 1.4164988109632946"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 100)",
            "value": 6202.565516153972,
            "unit": "ns",
            "range": "± 5.3615053430576625"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 100)",
            "value": 13110.375081380209,
            "unit": "ns",
            "range": "± 87.95164740664927"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.NoString(Count: 1000)",
            "value": 36657.842272949216,
            "unit": "ns",
            "range": "± 32.481945713394325"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_NoPool(Count: 1000)",
            "value": 59395.650714111325,
            "unit": "ns",
            "range": "± 114.93179489145791"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderPoolingBenchmarks.WithString_Pooled(Count: 1000)",
            "value": 134054.93798828125,
            "unit": "ns",
            "range": "± 36.36835757578985"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 100)",
            "value": 6609.072232055664,
            "unit": "ns",
            "range": "± 8.203401745960038"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 100)",
            "value": 6727.514263916016,
            "unit": "ns",
            "range": "± 22.895367205338385"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.ByteReader_Read(Count: 1000)",
            "value": 62837.27163357205,
            "unit": "ns",
            "range": "± 54.12134832347721"
          },
          {
            "name": "Benchmarks.Perf.ByteReaderStreamBenchmarks.CharReader_Read(Count: 1000)",
            "value": 63114.141821289064,
            "unit": "ns",
            "range": "± 88.55823874750605"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 53.96010576022996,
            "unit": "ns",
            "range": "± 0.15284375341028553"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"Jane Smith 28   55000.00 \")",
            "value": 76.23755237791273,
            "unit": "ns",
            "range": "± 0.0575071069353852"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_NoPool(Line: \"John Doe   30   60000.00 \")",
            "value": 52.27759563260608,
            "unit": "ns",
            "range": "± 0.13117439934054712"
          },
          {
            "name": "Benchmarks.Perf.ParserBenchmarks.Parse_WithStringPool(Line: \"John Doe   30   60000.00 \")",
            "value": 69.17790675163269,
            "unit": "ns",
            "range": "± 0.057983956836546946"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 100)",
            "value": 8075.901727294922,
            "unit": "ns",
            "range": "± 11.519675829080333"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 100)",
            "value": 18902.49549255371,
            "unit": "ns",
            "range": "± 131.42434232334256"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 100)",
            "value": 55092.49298095703,
            "unit": "ns",
            "range": "± 57.616626792930894"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Stream_ReadAsync(Count: 1000)",
            "value": 77936.12406412761,
            "unit": "ns",
            "range": "± 53.24155533765838"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync(Count: 1000)",
            "value": 191738.14834594727,
            "unit": "ns",
            "range": "± 72.68806252708059"
          },
          {
            "name": "Benchmarks.Perf.PipeReaderBenchmarks.Pipe_ReadAsync_SmallSegments(Count: 1000)",
            "value": 549771.4899902344,
            "unit": "ns",
            "range": "± 556.1942278215175"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 100)",
            "value": 4270.457779693604,
            "unit": "ns",
            "range": "± 30.94445069942949"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 100)",
            "value": 6188.684133148194,
            "unit": "ns",
            "range": "± 34.220027506663385"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 100)",
            "value": 6600.989404466417,
            "unit": "ns",
            "range": "± 57.40891467964504"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 100)",
            "value": 8489.906080627441,
            "unit": "ns",
            "range": "± 18.219209116133985"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 100)",
            "value": 10516.856880187988,
            "unit": "ns",
            "range": "± 38.002009943365785"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read(Count: 1000)",
            "value": 43150.820703125,
            "unit": "ns",
            "range": "± 233.93763929592347"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read(Count: 1000)",
            "value": 61213.763699001734,
            "unit": "ns",
            "range": "± 96.63970772412326"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.Naive_ReadLine(Count: 1000)",
            "value": 61900.24192979601,
            "unit": "ns",
            "range": "± 270.8266763934462"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.GeneratedReader_Read_Pooled(Count: 1000)",
            "value": 83448.71018981934,
            "unit": "ns",
            "range": "± 54.456390088498964"
          },
          {
            "name": "Benchmarks.Perf.ReaderBenchmarks.SpanReader_Read_Pooled(Count: 1000)",
            "value": 98499.14753417969,
            "unit": "ns",
            "range": "± 206.33275557797182"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.GeneratedReader_ReadOne",
            "value": 90.4553590297699,
            "unit": "ns",
            "range": "± 0.22575312302352613"
          },
          {
            "name": "Benchmarks.Perf.ReaderStartupBenchmarks.ReflectionReader_ConstructAndReadOne",
            "value": 129.87971971035003,
            "unit": "ns",
            "range": "± 0.7162294844053382"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Reflection(Line: \"John Doe  30   6000000.00\")",
            "value": 46.93274474143982,
            "unit": "ns",
            "range": "± 0.12139655942345864"
          },
          {
            "name": "Benchmarks.Perf.SourceGenParserBenchmarks.Generated(Line: \"John Doe  30   6000000.00\")",
            "value": 62.08527013990614,
            "unit": "ns",
            "range": "± 0.1489463722375602"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1)",
            "value": 121.38318376541137,
            "unit": "ns",
            "range": "± 0.19442642407819977"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1)",
            "value": 131.15744829177856,
            "unit": "ns",
            "range": "± 0.11700071674375806"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1)",
            "value": 167.2403142982059,
            "unit": "ns",
            "range": "± 0.35581740496503367"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1)",
            "value": 262.77581225501166,
            "unit": "ns",
            "range": "± 5.497202426463645"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1)",
            "value": 544.0576820373535,
            "unit": "ns",
            "range": "± 8.980841471496259"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 100)",
            "value": 12592.145463731555,
            "unit": "ns",
            "range": "± 16.1709957472709"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 100)",
            "value": 12599.774057006836,
            "unit": "ns",
            "range": "± 11.25205762334303"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 100)",
            "value": 12994.446321105957,
            "unit": "ns",
            "range": "± 30.758997165617135"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 100)",
            "value": 14255.632237243652,
            "unit": "ns",
            "range": "± 27.03699949708386"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 100)",
            "value": 16037.230411529541,
            "unit": "ns",
            "range": "± 14.124923037953097"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriter(Count: 1000)",
            "value": 122519.35906304253,
            "unit": "ns",
            "range": "± 159.5161857319447"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_ReuseWriterSpan(Count: 1000)",
            "value": 124441.37269422744,
            "unit": "ns",
            "range": "± 269.2123572090933"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_NewStream(Count: 1000)",
            "value": 125804.94805908203,
            "unit": "ns",
            "range": "± 126.03249093005535"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncNewStream(Count: 1000)",
            "value": 140593.78350830078,
            "unit": "ns",
            "range": "± 72.04711708197537"
          },
          {
            "name": "Benchmarks.Perf.WriterBenchmarks.WriteMany_AsyncReuseWriter(Count: 1000)",
            "value": 155851.29887695314,
            "unit": "ns",
            "range": "± 108.34923206313044"
          }
        ]
      }
    ]
  }
}
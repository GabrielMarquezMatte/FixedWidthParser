window.BENCHMARK_DATA = {
  "lastUpdate": 1781506601110,
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
      }
    ]
  }
}
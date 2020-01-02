# HandBrake Batch Runnerについて
HandbreakCLIを使って動画ファイルをまとめて変換するアプリです。

Handbreakはとても優秀な動画変換ソフトです。
しかしまとめて変換したいときには少し不便です。
これを解決するためにGUIで指定したものを連続実行することで便利に動画を変換するツールになります。

個人用としてVB.NET+WinFormで作っていましたが、
勉強をしたいので.NET Core+WPFで作り直して公開しようと思います。

# 使い方
![operation](https://user-images.githubusercontent.com/51582636/71642448-a331a600-2cee-11ea-9957-fcb2422b36db.gif)

1. 変換設定などを先にしておく(HandbrakeCLIのコマンドテンプレートを変更します)
2. ファイルの変換先などを設定する
3. 変換したいファイルをドラックアンドドロップする
4. 変換開始をクリックする

# 便利な機能
- 変換完了フォルダを指定しておくと変換完了したファイルが移動される
- 既に同名の変換ファイルがあると変換をスキップする(2重変換防止)
- 次にキャンセルボタンでキャンセルすると変換途中のファイルが完了後にキャンセル
- 変換中にでもファイルの追加、削除ができる
- ファイルリストは保存し、読み込むことで再開することができる(変換完了しているファイルはスキップ)
- フォルダ監視設定をすると、自動でファイルの変換を行うことができる

# 実装予定
- マルチランゲージ
- マテリアルデザイン

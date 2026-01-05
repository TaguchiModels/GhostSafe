# GhostSafe
PCに保存している大切なファイルや家族にも見せたくない情報、誰にも知られたくないデータは、誰しもあるものです。<br>
また、万が一ウィルスに感染した際、金融アカウント情報などを抜き取られると死活問題になります。  
これらの情報を守る手段として、既存の暗号化ソフトを使えば保護できますが、大容量の動画やデータファイルの場合、暗号化・復号に時間がかかり、閲覧や編集のたびに操作が面倒です。 <br>
そこで開発したのが本ソフトウェアです。本ソフトを使えば、1ギガバイトを超えるような大容量ファイルでも、数十秒で暗号化・復号化が完了します。<br>
作業効率を落とさずに安全に管理できます。<br>

【ソフトウェアの機能】<br>
・完全フリーウェアです<br>
・ソフトウェアはパスワードの入力により起動します<br>
・エクスプローラー風のUI画面でドラッグアンドドロップによりファイルを暗号化します<br>
・UI画面の左ペインはフォルダを階層構造で作成したり削除したりできます。<br>
・UI画面の右ペインはフォルダ内のファイルを表示したり削除したりできます。<br>
・UI画面の右ペインのファイルは左ペインのフォルダにドラッグアンドドロップすることで移動することができます。<br>
・暗号化したファイルはダブルクリックする事により複合し、表示することができます。<br>
・ダブルクリックしたファイルが動画ファイルの場合は、アプリ専用のウィンドウにより動画を再生します。<br>
・ダブルクリックしたファイルが画像ファイルの場合は、アプリ専用のウィンドウにより静止画を表示します。<br>
・複合化したファイルが動画、静止画伊賀の場合は、拡張子に応じたアプリが起動されます。その際、例えばExcelを開いて内容を書き換えた場合には、書き換えた内容で暗号化し保存することができます。<br>
・アプリ専用のウィンドウ以外が起動された場合（例えばExcelやメモ帳など）は、暗号化されたファイル名が表示され、元のファイル名はわからないようになっています。<br>
・アプリを終了すると複合化したファイルは全て削除されます。<br>

具体的な操作方法は[こちら](./docs/operation.md)を読んでください。  

--- 

### 【アプリ名：GhostSafe】<br>
Copyright (c) 2026 Taguchi Models<br>
<br>
本ソフトウェアは、以下のオープンソースライブラリを使用しています。<br>
それぞれのライセンス条件に従い配布されています。<br>

--- 

### オープンソースライブラリについて
#### 1. VirtualizingWrapPanel 2.3.1<br>
License: MIT License<br>
Copyright (c) 2011-2023 VirtualizingWrapPanel Contributors<br>
https://github.com/GuOrg/Gu.Wpf.VirtualizingWrapPanel<br>
Permission is hereby granted, free of charge, to any person obtaining a copy<br>
of this software and associated documentation files (the "Software"), to deal<br>
in the Software without restriction, including without limitation the rights<br>
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell<br>
copies of the Software, and to permit persons to whom the Software is<br>
furnished to do so, subject to the following conditions:<br>

#### 2. VideoLAN.LibVLC.Windows 3.0.21<br>
License: GNU Lesser General Public License v2.1 (LGPL)<br>
Copyright (c) 1996-2024 VideoLAN and authors<br>
<br>
LibVLC and its .NET bindings (LibVLCSharp) are licensed under the LGPL.<br>
You may redistribute and/or modify it under the terms of the LGPL.<br>
A copy of the license is available at:<br>
https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html<br>
<br>
※ 動的リンクにより使用しています。ユーザーはライブラリを差し替えることが可能です。<br>

#### 3. LibVLCSharp.WPF 3.9.3<br>
License: GNU Lesser General Public License v2.1 (LGPL)<br>
Copyright (c) 2017-2024 VideoLAN and authors<br>
https://github.com/videolan/libvlcsharp<br>
<br>
A copy of the license is available at:<br>
https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html<br>

#### 4. System.Drawing.Common 9.0.8<br>
License: MIT License<br>
Copyright (c) Microsoft Corporation<br>
https://github.com/dotnet/runtime<br>

#### 5. PdfPig 0.0.11<br>
License: MIT License<br>
Copyright (c) 2017-2023 UglyToad<br>
https://github.com/UglyToad/PdfPig<br>

#### 6. DirectShowLib 1.0.0<br>
License: GNU Lesser General Public License v2.1 (LGPL)<br>
Copyright (c) DirectShowLib Contributors<br>
https://sourceforge.net/projects/directshownet/<br>
A copy of the license is available at:<br>
https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html<br>

#### 7. AudioSwitcher.AudioApi.CoreAudio 3.0.3<br>
License: MIT License<br>
Copyright (c) 2015 AudioSwitcher Team<br>
https://github.com/AudioSwitcher/AudioSwitcher<br>

本ソフトウェアは、上記ライブラリを組み合わせて利用しています。<br>
各ライセンス条項に基づき、すべての著作権表示およびライセンス文を保持しています。<br>

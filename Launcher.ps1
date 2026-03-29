Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Open-Edition {
    param(
        [string]$Edition
    )

    switch ($Edition) {
        'HTML' {
            $target = Join-Path $scriptRoot 'classic-html\index.html'
        }
        '+E+RIS' {
            $target = Join-Path $scriptRoot 'enhanced\dist\index.html'
        }
        default {
            return
        }
    }

    if (-not (Test-Path -LiteralPath $target)) {
        [System.Windows.Forms.MessageBox]::Show(
            ("The selected edition is not available yet:`n{0}" -f $target),
            'Launch Tetris',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        ) | Out-Null
        return
    }

    Start-Process $target | Out-Null
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Launch Tetris'
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.ClientSize = New-Object System.Drawing.Size(320, 160)
$form.TopMost = $true

$label = New-Object System.Windows.Forms.Label
$label.Text = 'Version'
$label.Location = New-Object System.Drawing.Point(20, 20)
$label.AutoSize = $true

$combo = New-Object System.Windows.Forms.ComboBox
$combo.Location = New-Object System.Drawing.Point(20, 45)
$combo.Size = New-Object System.Drawing.Size(280, 28)
$combo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
[void]$combo.Items.Add('HTML')
[void]$combo.Items.Add('+E+RIS')
$combo.SelectedIndex = 0

$runButton = New-Object System.Windows.Forms.Button
$runButton.Text = 'Run'
$runButton.Location = New-Object System.Drawing.Point(20, 88)
$runButton.Size = New-Object System.Drawing.Size(280, 32)
$runButton.Add_Click({
    Open-Edition -Edition ([string]$combo.SelectedItem)
    $form.Close()
})

$form.Controls.Add($label)
$form.Controls.Add($combo)
$form.Controls.Add($runButton)

[void]$form.ShowDialog()

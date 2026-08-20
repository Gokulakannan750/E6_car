import sys
sys.stdout.reconfigure(encoding="utf-8")
path = "src/E6CarSpa.Desktop/Views/ShellWindow.xaml"
with open(path, "r", encoding="utf-8") as f:
	lines = f.readlines()
for i, line in enumerate(lines):
	if "Showrooms" in line and "Button" in line:
		# Keep the content, just fix leading whitespace
		content = line.lstrip()
		lines[i] = " " + content
		with open(path, "w", encoding="utf-8") as f:
			f.writelines(lines)
		print("Fixed line " + str(i + 1))
		print("Content: " + lines[i].rstrip())
		break

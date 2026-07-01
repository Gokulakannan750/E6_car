import os, re

dir_path = r'e:\TTS\Projects\Web_App & Applications\E6_car_care\src\E6CarSpa.Mobile\Pages'
for root, _, files in os.walk(dir_path):
    for f in files:
        if f.endswith('.xaml'):
            path = os.path.join(root, f)
            with open(path, 'r', encoding='utf-8') as file:
                content = file.read()
            
            # Map specific colors to semantic
            content = content.replace('TextColor="#FF6B6B"', 'TextColor="{AppThemeBinding Light={StaticResource Error}, Dark={StaticResource Error}}"')
            content = content.replace('TextColor="#FF8A65"', 'TextColor="{AppThemeBinding Light={StaticResource Warning}, Dark={StaticResource Warning}}"')
            content = content.replace('TextColor="#7CC47C"', 'TextColor="{AppThemeBinding Light={StaticResource Success}, Dark={StaticResource Success}}"')
            
            # Remove other hardcoded colors
            content = re.sub(r'\sBackgroundColor="#[0-9A-Fa-f]{3,8}"', '', content)
            content = re.sub(r'\sTextColor="#[0-9A-Fa-f]{3,8}"', '', content)
            content = re.sub(r'\sTextColor="White"', '', content)
            content = re.sub(r'\sPlaceholderColor="#[0-9A-Fa-f]{3,8}"', '', content)
            content = re.sub(r'\sColor="#[0-9A-Fa-f]{3,8}"', '', content)
            
            # Add Style to Borders
            content = re.sub(r'<Border(?!.*Style=)', r'<Border Style="{StaticResource CardBorder}"', content)
            
            with open(path, 'w', encoding='utf-8') as file:
                file.write(content)

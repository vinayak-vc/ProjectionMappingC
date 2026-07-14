import os
import markdown
from fpdf import FPDF, HTMLMixin

class MyFPDF(FPDF, HTMLMixin):
    pass

def convert_md_to_pdf(md_path, pdf_path):
    with open(md_path, 'r', encoding='utf-8') as f:
        md_text = f.read()

    # Convert Markdown to HTML
    html_text = markdown.markdown(md_text)

    # Basic styling so fpdf doesn't fail on missing tags or sizes
    html_text = f"""
    <font face="Arial" size="10">
    {html_text}
    </font>
    """

    pdf = MyFPDF()
    # Add Arial font to avoid encoding issues
    pdf.add_page()
    pdf.set_font("Arial", size=10)
    pdf.write_html(html_text)
    pdf.output(pdf_path)
    print(f"Generated {pdf_path}")

if __name__ == "__main__":
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    
    unity_md = os.path.join(root_dir, 'bindings', 'unity', 'README.md')
    unity_pdf = os.path.join(root_dir, 'bindings', 'unity', 'README.pdf')
    if os.path.exists(unity_md):
        convert_md_to_pdf(unity_md, unity_pdf)

    unreal_md = os.path.join(root_dir, 'bindings', 'unreal', 'README.md')
    unreal_pdf = os.path.join(root_dir, 'bindings', 'unreal', 'README.pdf')
    if os.path.exists(unreal_md):
        convert_md_to_pdf(unreal_md, unreal_pdf)

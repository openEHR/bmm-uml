using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using R4C_BMM_EA_Extension.EA_BMM_Model;
using System.IO;

namespace R4C_BMM_EA_Extension
{
    class BMM_ModelVisitor : IVisitor
    {
        StringBuilder bmm = new StringBuilder();
        int depth = 0;
        mode pass_mode;
        enum mode { package_mode, class_mode, primitives_mode}

        public void run(BMM_Package package)
        {
            string fileName = string.Format(@"d:\tmp\{0}-{1}-{2}-generated-from-UML.bmm", package.rm_publisher, package.schema_name, package.rm_release);
            fileName = showFileDialog("Select output BMM filename", "bmm files (*.bmm)|*.bmm", fileName, false);
            if (fileName == String.Empty)
            {
                return;
            }

            /*
            ------------------------------------------------------
            -- BMM version on which these schemas are based.
            -- Current BMM version can be found as value of 'Bmm_internal_version' in 
            --     http://www.openehr.org/svn/ref_impl_eiffel/BRANCHES/adl1.5/libraries/common_libs/src/basic_meta_model/bmm_definitions.e
            --
            ------------------------------------------------------
             */
            appendLine("-- xxx");
            appendLine("bmm_version", "2.0");

            appendCommentBlock("schema identification", "(schema_id computed as <rm_publisher>_<schema_name>_<rm_release>)");
            appendLine("rm_publisher", package.rm_publisher);
            appendLine("schema_name", package.schema_name);
            appendLine("rm_release", package.rm_release);

            appendCommentBlock("schema documentation");
            appendLine("schema_revision", DateTime.Now.ToString("D"));
            appendLine("schema_lifecycle_state", package.schema_lifecycle_state);
            appendLine("schema_description", string.Format("{0} v{1} schema generated from UML", package.name, package.rm_release));

/*            appendCommentBlock("inclusions");
            appendLine("includes = <");
            appendLine("    [\"1\"] = <");
            appendLine("         id = <\"openehr_primitive_types_1.0.2\">"); // do we need to map between UML primitives and openehr primitives??
            appendLine("    >");
            appendLine(">");*/

            appendCommentBlock("archetyping");
            foreach (BMM_Package cpackage in package.packages)
            {
                if (cpackage.is_archetype_rm_closure_package)
                {
                    foreach (BMM_Class pclass in cpackage.classes)
                    {
                        if (pclass.is_archetype_parent)
                        {
                            appendLine("archetype_parent_class", pclass.name);
                        }
                    }
                    appendLine(string.Format("archetype_rm_closure_packages = <\"{0}.{1}\", ...>", package.name, cpackage.name));
                }
            }

            pass_mode = mode.package_mode;
            appendCommentBlock("packages");
            appendLine();
            appendLine("packages = <");
            package.accept(this);
            appendLine(">");

            pass_mode = mode.class_mode;
            appendCommentBlock("classes");
            appendLine();
            appendLine("class_definitions = <");
            depth++;
            package.accept(this);
            depth--;
            appendLine(">");

            pass_mode = mode.primitives_mode;
            appendCommentBlock("primitive types");
            appendLine();
            appendLine("primitive_types = <");
            depth++;
            package.accept(this);
            depth--;
            appendLine(">");

            // save to file
            using (StreamWriter stream = new StreamWriter(fileName))
            {
                stream.Write(bmm.ToString());
            }

            MessageBox.Show("BMM Ready.");
        }

        public void visit(BMM_Package package)
        {
            if (pass_mode == mode.package_mode)
            {
                depth++;
                inset(); bmm.Append("[\"").Append(package.name).AppendLine("\"] = <");
                depth++;
                appendLine("name", package.name);
                if (package.has_classes)
                {
                    List<string> classes = new List<string>();
                    foreach (BMM_Class child in package.classes)
                    {
                        classes.Add(child.name);
                    }
                    appendLine("classes", string.Join("\", \"", classes.ToArray()));
                }
                if (package.has_child_packages)
                {
                    appendLine("packages = <");
                    foreach (BMM_Package child in package.packages)
                    {
                        child.accept(this);
                    }
                    appendLine(">");
                }
                depth--;
                appendLine(">");
                depth--;
            }
            else if (pass_mode == mode.class_mode)
            {
                foreach (BMM_Package child in package.packages)
                {
                    child.accept(this);
                }
                if (!package.is_primitive_types_package && package.has_classes)
                {
                    appendCommentBlock(package.name);
                    appendLine();
                    foreach (BMM_Class child in package.classes)
                    {
                        child.accept(this);
                    }
                }
            }
            else if (pass_mode == mode.primitives_mode)
            {
                foreach (BMM_Package child in package.packages)
                {
                    child.accept(this);
                }
                if (package.is_primitive_types_package && package.has_classes)
                {
                    foreach (BMM_Class child in package.classes)
                    {
                        child.accept(this);
                    }
                }
            }
        }

        public void visit(BMM_Class bmm_class)
        {
            inset(); bmm.Append("[\"").Append(bmm_class.name).AppendLine("\"] = <");
            depth++;
            appendLine("name", bmm_class.name);

            if (bmm_class.has_ancestors)
            {
                List<string> ancestors = new List<string>();
                foreach (BMM_Class parent in bmm_class.ancestors)
                {
                    ancestors.Add(parent.name);
                }
                inset(); bmm.Append("ancestors").Append(" = <\"").Append(string.Join("\", \"", ancestors.ToArray())).AppendLine("\", ...>");
            }
            if (bmm_class.is_open)
            {
                appendLine("generic_parameter_defs = <");
                appendLine("    [\"T\"] = <");
                appendLine("        name = <\"T\">");
                appendLine("    >");
                appendLine(">");
            }
            if (bmm_class.is_abstract)
            {
                appendLine("is_abstract", bmm_class.is_abstract);
            }
            if (bmm_class.has_properties)
            {
                inset(); bmm.AppendLine("properties = <");
                depth++;
                foreach (BMM_Property property in bmm_class.properties)
                {
                    inset(); bmm.Append("[\"").Append(property.name).Append("\"] = (").Append(property.kind).AppendLine(") <");
                    depth++;
                    appendLine("name", property.name);
                    switch (property.kind)
                    {
                        case BMM_Property.P_BMM_CONTAINER_PROPERTY:
                            appendLine("type_def = <");
                            depth++;
                            appendLine("container_type", property.container_type);
                            appendLine("type", property.type);
                            depth--;
                            appendLine(">");
                            break;
                        case BMM_Property.P_BMM_SINGLE_PROPERTY:
                            appendLine("type", property.type);
                            break;
                        case BMM_Property.P_BMM_SINGLE_PROPERTY_OPEN:
                            appendLine("type", "T");
                            break;
                        case BMM_Property.P_BMM_GENERIC_PROPERTY:
                            /* e.g.
				                type_def = <
					                root_type = <"DV_INTERVAL">
					                generic_parameters = <"DV_DATE_TIME">
				                >
                             */
                            int lt = property.type.IndexOf('<');
                            string root_type = property.type.Substring(0, lt);
                            string generic_parameters = property.type.Substring(lt + 1, property.type.Length - lt - 2);
                            appendLine("type_def = <");
                            depth++;
                            appendLine("root_type", root_type);
                            appendLine("generic_parameters", generic_parameters);
                            depth--;
                            appendLine(">");
                            break;
                    }

                    if (!string.IsNullOrEmpty(property.cardinality))
                    {
                        inset(); bmm.Append("cardinality = <|").Append(property.cardinality).AppendLine("|>");
                    }
                    if (property.is_mandatory)
                    {
                        appendLine("is_mandatory", true);
                    }
                    if (property.is_computed)
                    {
                        appendLine("is_computed", true);
                    }
                    if (property.is_im_runtime)
                    {
                        appendLine("is_im_runtime", true);
                    }
                    if (property.is_im_infrastructure)
                    {
                        appendLine("is_im_infrastructure", true);
                    }
                    depth--;
                    appendLine(">");
                }
                depth--;
                appendLine(">");
            }
            depth--;
            appendLine(">");
            appendLine();
        }

        private void inset()
        {
            for (int d = 0; d < depth; d++)
            {
                bmm.Append("    ");
            }
        }

        private void appendLine()
        {
            bmm.AppendLine();
        }

        private void appendCommentBlock(params string[] comments)
        {
            appendLine();
            appendLine("------------------------------------");
            foreach (string comment in comments)
            {
                inset(); bmm.Append("-- ").AppendLine(comment);
            }
            appendLine("------------------------------------");
        }

        private void appendLine(string line)
        {
            inset(); bmm.AppendLine(line);
        }

        private void appendLine(string key, string value)
        {
            inset(); bmm.Append(key).Append(" = <\"").Append(value).AppendLine("\">");
        }

        private void appendLine(string key, bool value)
        {
            inset(); bmm.Append(key).Append(" = <").Append(value).AppendLine(">");
        }

        public string showFileDialog(string title, string filter, string fileName, bool open)
        {
            FileDialog dialog;
            if (open)
            {
                dialog = new OpenFileDialog();
            }
            else
            {
                dialog = new SaveFileDialog();
            }
            dialog.Filter = filter;
            dialog.Title = title;
            dialog.InitialDirectory = Path.GetDirectoryName(fileName);
            dialog.FileName = Path.GetFileName(fileName);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.FileName;
            }
            else
            {
                return String.Empty;
            }
        }
    }
}

# TAGGAR: General-Purpose Task Guidance from Natural Language in Augmented Reality using Vision-Language Models

- Unity code running on HoloLens 2 is located in the `Assets/Scripts` directory
- Python code running on server is located in the `object_detection_scripts` directory

### **TAGGAR published in ACM SUI 2024:** https://dl-acm-org.ezproxy.lib.vt.edu/doi/10.1145/3677386.3682095

- This research done under Dr. Doug Bowman at Virginia Tech's [3D Interaction Group]([url](https://wordpress.cs.vt.edu/3digroup/)), as part of my Master's Thesis.
- Video demo (2 minutes) of our system is attached to publication link

Abstract: Augmented reality (AR) task guidance systems provide assistance for procedural tasks by rendering virtual guidance visuals within the real-world environment. Current AR task guidance systems are limited in that they require AR system experts to manually place visuals, require models of real-world objects, or only function for limited tasks or environments. We propose a general-purpose AR task guidance approach for tasks defined by natural language. Our approach allows an operator to take pictures of relevant objects and write task instructions for an end user, which are used by the system to determine where to place guidance visuals. Then, an end user can receive and follow guidance even if objects change locations or environments. Our approach utilizes current vision-language machine learning models for text and image semantic understanding and object localization. We built a proof-of-concept system called TAGGAR using our approach and tested its accuracy and usability in a user study. We found that all operators were able to generate clear guidance for tasks and end users were able to follow the guidance visuals to complete the expected action 85.7% of the time without any knowledge of the tasks.

## System Overview
![operator_and_user_intro_diagram.pdf](https://github.com/user-attachments/files/17085512/operator_and_user_intro_diagram.pdf)

## System Diagram
![system_design_diagram.pdf](https://github.com/user-attachments/files/17085514/system_design_diagram.pdf)
